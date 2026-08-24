using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Tests;

public sealed class LedgerUseCaseTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Money SummitGoldPrice = Money.Of(120.75m, Currency.Usd);

    [Fact]
    public async Task Earn_then_burn_then_expire_leaves_the_worked_balance()
    {
        var world = World.Summit();

        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 500, "earn-1", "Opening grant"), CancellationToken.None);
        await world.Burn.ExecuteAsync(
            new BurnCreditsCommand(world.Maya.Id, 200, SummitGoldPrice, "burn-1", "Booking tender"),
            CancellationToken.None);
        await world.Expire.ExecuteAsync(
            new ExpireCreditsCommand(world.Maya.Id, 50, "expire-1", "Lapsed"),
            CancellationToken.None);

        world.Balance(world.Maya.Id).Should().Be(250);
        world.Ledger.Transactions.Should().HaveCount(3);
        world.Ledger.Transactions.Should().OnlyContain(transaction => transaction.Entries.Sum(e => e.Amount) == 0);
    }

    [Fact]
    public async Task Reversal_of_a_burn_restores_the_exact_original_amounts()
    {
        var world = World.Summit();

        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 500, "earn-1", "Opening grant"), CancellationToken.None);
        var burned = await world.Burn.ExecuteAsync(
            new BurnCreditsCommand(world.Maya.Id, 200, SummitGoldPrice, "burn-1", "Booking tender"),
            CancellationToken.None);

        var reversed = await world.Reverse.ExecuteAsync(
            new ReverseLedgerCommand(burned.Value.Transaction.Id, "rev-1", "Cancel booking"),
            CancellationToken.None);

        reversed.IsSuccess.Should().BeTrue();
        reversed.Value.Transaction.ReversesTransactionId.Should().Be(burned.Value.Transaction.Id);
        reversed.Value.Transaction.Entries.Select(e => e.Amount)
            .Should()
            .Equal(burned.Value.Transaction.Entries.Select(e => -e.Amount));
        world.Balance(world.Maya.Id).Should().Be(500);
    }

    [Fact]
    public async Task Same_earn_key_replays_without_a_second_posting()
    {
        var world = World.Summit();
        var command = new EarnCreditsCommand(world.Maya.Id, 500, "earn-1", "Opening grant");

        var first = await world.Earn.ExecuteAsync(command, CancellationToken.None);
        var replay = await world.Earn.ExecuteAsync(command, CancellationToken.None);

        first.Value.IsReplay.Should().BeFalse();
        replay.Value.IsReplay.Should().BeTrue();
        replay.Value.Transaction.Id.Should().Be(first.Value.Transaction.Id);
        world.Ledger.Transactions.Should().ContainSingle();
        world.Balance(world.Maya.Id).Should().Be(500);
    }

    [Fact]
    public async Task Burn_above_the_partner_cap_is_rejected()
    {
        var world = World.Summit();
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 10_000, "earn-1", "Opening grant"), CancellationToken.None);

        var result = await world.Burn.ExecuteAsync(
            new BurnCreditsCommand(world.Maya.Id, 4831, SummitGoldPrice, "burn-1", "Over cap"),
            CancellationToken.None);

        result.Error.Should().Be(Errors.BurnCapExceeded);
        world.Ledger.Transactions.Should().ContainSingle(t => t.Type == LedgerTransactionType.Earn);
    }

    [Fact]
    public async Task Burn_at_the_worked_example_cap_is_accepted()
    {
        var world = World.Summit();
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 6_000, "earn-1", "Opening grant"), CancellationToken.None);

        var result = await world.Burn.ExecuteAsync(
            new BurnCreditsCommand(world.Maya.Id, 4830, SummitGoldPrice, "burn-1", "Max tender"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        world.Balance(world.Maya.Id).Should().Be(1170);
    }

    [Fact]
    public async Task Burn_above_balance_is_rejected()
    {
        var world = World.Summit();
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 100, "earn-1", "Opening grant"), CancellationToken.None);

        var result = await world.Burn.ExecuteAsync(
            new BurnCreditsCommand(world.Maya.Id, 200, SummitGoldPrice, "burn-1", "Too much"),
            CancellationToken.None);

        result.Error.Should().Be(Errors.InsufficientCredits);
    }

    [Fact]
    public async Task Expire_is_an_explicit_posting_and_cannot_overdraw()
    {
        var world = World.Summit();
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 50, "earn-1", "Opening grant"), CancellationToken.None);

        var expired = await world.Expire.ExecuteAsync(
            new ExpireCreditsCommand(world.Maya.Id, 50, "expire-1", "Lapsed"),
            CancellationToken.None);
        var over = await world.Expire.ExecuteAsync(
            new ExpireCreditsCommand(world.Maya.Id, 1, "expire-2", "Nothing left"),
            CancellationToken.None);

        expired.IsSuccess.Should().BeTrue();
        expired.Value.Transaction.Type.Should().Be(LedgerTransactionType.Expire);
        over.Error.Should().Be(Errors.InsufficientCredits);
        world.Balance(world.Maya.Id).Should().Be(0);
    }

    [Fact]
    public async Task Reversing_an_earn_after_a_spend_does_not_go_negative()
    {
        var world = World.Summit();
        var earned = await world.Earn.ExecuteAsync(
            new EarnCreditsCommand(world.Maya.Id, 500, "earn-1", "Opening grant"),
            CancellationToken.None);
        await world.Burn.ExecuteAsync(
            new BurnCreditsCommand(world.Maya.Id, 200, SummitGoldPrice, "burn-1", "Booking tender"),
            CancellationToken.None);

        var result = await world.Reverse.ExecuteAsync(
            new ReverseLedgerCommand(earned.Value.Transaction.Id, "rev-1", "Clawback"),
            CancellationToken.None);

        result.Error.Should().Be(Errors.InsufficientCredits);
        world.Balance(world.Maya.Id).Should().Be(300);
    }

    [Fact]
    public async Task A_transaction_cannot_be_reversed_twice()
    {
        var world = World.Summit();
        var earned = await world.Earn.ExecuteAsync(
            new EarnCreditsCommand(world.Maya.Id, 500, "earn-1", "Opening grant"),
            CancellationToken.None);
        await world.Reverse.ExecuteAsync(
            new ReverseLedgerCommand(earned.Value.Transaction.Id, "rev-1", "Clawback"),
            CancellationToken.None);

        var second = await world.Reverse.ExecuteAsync(
            new ReverseLedgerCommand(earned.Value.Transaction.Id, "rev-2", "Again"),
            CancellationToken.None);

        second.Error.Should().Be(Errors.TransactionAlreadyReversed);
        world.Ledger.Transactions.Count(t => t.Type == LedgerTransactionType.Reversal).Should().Be(1);
        world.Balance(world.Maya.Id).Should().Be(0);
    }

    [Fact]
    public async Task Unknown_original_is_not_found()
    {
        var world = World.Summit();

        var result = await world.Reverse.ExecuteAsync(
            new ReverseLedgerCommand(LedgerTransactionId.New(), "rev-1", "Missing"),
            CancellationToken.None);

        result.Error.Should().Be(Errors.LedgerTransactionNotFound);
    }

    [Fact]
    public async Task Adjustment_credits_like_an_earn_and_cannot_overdraw()
    {
        var world = World.Summit();
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 40, "earn-1", "Opening grant"), CancellationToken.None);

        var up = await world.Adjust.ExecuteAsync(
            new AdjustCreditsCommand(world.Maya.Id, 10, "adj-1", "Goodwill"),
            CancellationToken.None);
        var down = await world.Adjust.ExecuteAsync(
            new AdjustCreditsCommand(world.Maya.Id, -100, "adj-2", "Clawback"),
            CancellationToken.None);

        up.IsSuccess.Should().BeTrue();
        world.Balance(world.Maya.Id).Should().Be(50);
        down.Error.Should().Be(Errors.InsufficientCredits);
    }

    [Fact]
    public async Task Another_partners_member_is_not_found()
    {
        var world = World.Summit();

        var result = await world.Earn.ExecuteAsync(
            new EarnCreditsCommand(MemberId.New(), 10, "earn-1", "Grant"),
            CancellationToken.None);

        result.Error.Should().Be(Errors.MemberNotFound);
        world.Ledger.Transactions.Should().BeEmpty();
    }

    private sealed class World
    {
        private World(
            Member maya,
            FakeLedger ledger,
            EarnCredits earn,
            BurnCredits burn,
            ExpireCredits expire,
            ReverseLedger reverse,
            AdjustCredits adjust)
        {
            Maya = maya;
            Ledger = ledger;
            Earn = earn;
            Burn = burn;
            Expire = expire;
            Reverse = reverse;
            Adjust = adjust;
        }

        public Member Maya { get; }

        public FakeLedger Ledger { get; }

        public EarnCredits Earn { get; }

        public BurnCredits Burn { get; }

        public ExpireCredits Expire { get; }

        public ReverseLedger Reverse { get; }

        public AdjustCredits Adjust { get; }

        public int Balance(MemberId memberId)
        {
            var account = Ledger.FindAccountAsync(
                    Maya.PartnerId,
                    LedgerAccountType.MemberCredits,
                    memberId,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult()!;

            return LedgerBalances.For(account.Id, Ledger.Transactions);
        }

        public static World Summit()
        {
            var partner = Partner.Create(
                "SUMMIT",
                "Summit Rewards",
                Currency.Usd,
                new PartnerTheme("#BE185D", "#FFF7ED", "#1D4ED8"),
                new CreditPolicy(0.01m, Percent.From(40m), 730, Percent.From(10m)),
                new QuotePolicy(15, RateDriftPolicy.AbsorbWithinTolerance, Percent.From(2m)),
                new SagaPolicy(10, 3, 5, 60),
                new OpportunityPolicy(3, 14, 0.55m, Percent.From(10m), 2, 30, 7, new SignalWeights(0.2m, 0.2m, 0.2m, 0.2m, 0.2m)));
            var maya = Member.Create(partner.Id, "Maya", TierCode.Gold);
            var tenant = new FakeTenant { Current = TenantContext.ForMember(maya) };
            var ledger = new FakeLedger();
            var unitOfWork = new FakeUnitOfWork();
            var claim = new ClaimIdempotency(tenant, new FakeIdempotencyStore(), new FakeClock(AsOf));
            var members = new FakeMembers(maya);
            var partners = new FakePartners(partner);
            var clock = new FakeClock(AsOf);

            return new World(
                maya,
                ledger,
                new EarnCredits(tenant, members, partners, ledger, unitOfWork, claim, clock),
                new BurnCredits(tenant, members, partners, ledger, unitOfWork, claim, clock),
                new ExpireCredits(tenant, members, partners, ledger, unitOfWork, claim, clock),
                new ReverseLedger(tenant, members, partners, ledger, unitOfWork, claim, clock),
                new AdjustCredits(tenant, members, partners, ledger, unitOfWork, claim, clock));
        }
    }
}
