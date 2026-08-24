using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Tests;

public sealed class LedgerQueryTests
{
    private static readonly DateTimeOffset March1 = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset March15 = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Money Price = Money.Of(120.75m, Currency.Usd);

    [Fact]
    public async Task Balance_is_derived_and_includes_the_monetary_equivalent()
    {
        var world = World.Summit();
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 500, "earn-1", "Opening grant"), CancellationToken.None);

        var result = await world.Balance.ExecuteAsync(new GetBalanceQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Credits.Should().Be(500);
        result.Value.MonetaryValue.Should().Be(Money.Of(5.00m, Currency.Usd));
        result.Value.BurnCap.Should().Be(Percent.From(40m));
    }

    [Fact]
    public async Task Statement_shows_reason_running_balance_and_reversal_link()
    {
        var world = World.Summit();
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 500, "earn-1", "Opening grant"), CancellationToken.None);
        world.Clock.UtcNow = world.Clock.UtcNow.AddSeconds(1);
        var burned = await world.Burn.ExecuteAsync(
            new BurnCreditsCommand(world.Maya.Id, 200, Price, "burn-1", "Booking tender"),
            CancellationToken.None);
        world.Clock.UtcNow = world.Clock.UtcNow.AddSeconds(1);
        await world.Reverse.ExecuteAsync(
            new ReverseLedgerCommand(burned.Value.Transaction.Id, "rev-1", "Cancel booking"),
            CancellationToken.None);

        var statement = await world.Statement.ExecuteAsync(new GetStatementQuery(), CancellationToken.None);

        statement.Value.Balance.Should().Be(500);
        statement.Value.Lines.Should().HaveCount(3);
        statement.Value.Lines[0].RunningBalance.Should().Be(500);
        statement.Value.Lines[1].RunningBalance.Should().Be(300);
        statement.Value.Lines[2].RunningBalance.Should().Be(500);
        statement.Value.Lines[2].ReversesTransactionId.Should().Be(burned.Value.Transaction.Id);
        statement.Value.Lines.Should().OnlyContain(line => !string.IsNullOrWhiteSpace(line.Reason));
    }

    [Fact]
    public async Task Past_dated_liability_is_stable_under_later_activity()
    {
        var world = World.Summit();
        world.Clock.UtcNow = March15;
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 500, "earn-1", "Opening grant"), CancellationToken.None);
        await world.Burn.ExecuteAsync(
            new BurnCreditsCommand(world.Maya.Id, 200, Price, "burn-1", "Booking tender"),
            CancellationToken.None);
        await world.Expire.ExecuteAsync(new ExpireCreditsCommand(world.Maya.Id, 50, "expire-1", "Lapsed"), CancellationToken.None);

        var asOf = new DateOnly(2026, 3, 15);
        var first = await world.Liability.ExecuteAsync(new GetLiabilityReportQuery(asOf), CancellationToken.None);

        world.Clock.UtcNow = March15.AddDays(1);
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 100, "earn-2", "Later grant"), CancellationToken.None);

        var again = await world.Liability.ExecuteAsync(new GetLiabilityReportQuery(asOf), CancellationToken.None);

        first.Value.CreditsIssued.Should().Be(500);
        first.Value.CreditsBurned.Should().Be(200);
        first.Value.CreditsExpired.Should().Be(50);
        first.Value.CreditsOutstanding.Should().Be(250);
        first.Value.MonetaryLiability.Should().Be(Money.Of(2.50m, Currency.Usd));
        again.Value.Should().Be(first.Value);
        (await world.Balance.ExecuteAsync(new GetBalanceQuery(), CancellationToken.None)).Value.Credits.Should().Be(350);
    }

    [Fact]
    public async Task Reconcile_reports_a_gap_and_does_not_post_a_correction()
    {
        var world = World.Summit();
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 500, "earn-1", "Opening grant"), CancellationToken.None);
        await world.Burn.ExecuteAsync(
            new BurnCreditsCommand(world.Maya.Id, 200, Price, "burn-1", "Booking tender"),
            CancellationToken.None);

        world.Tenders.Tenders = 0;
        var gap = await world.Reconcile.ExecuteAsync(
            new ReconcileLedgerQuery(DateOnly.FromDateTime(March15.UtcDateTime)),
            CancellationToken.None);

        world.Tenders.Tenders = 200;
        var matched = await world.Reconcile.ExecuteAsync(
            new ReconcileLedgerQuery(DateOnly.FromDateTime(March15.UtcDateTime)),
            CancellationToken.None);

        gap.Value.IsBalanced.Should().BeFalse();
        gap.Value.Difference.Should().Be(200);
        matched.Value.IsBalanced.Should().BeTrue();
        world.Ledger.Transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Expire_due_worker_posts_fifo_lapse_and_leaves_younger_lots()
    {
        var world = World.ShortLived();
        world.Clock.UtcNow = March1;
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 100, "earn-1", "Old lot"), CancellationToken.None);
        world.Clock.UtcNow = March1.AddDays(4);
        await world.Earn.ExecuteAsync(new EarnCreditsCommand(world.Maya.Id, 50, "earn-2", "Young lot"), CancellationToken.None);
        await world.Burn.ExecuteAsync(
            new BurnCreditsCommand(world.Maya.Id, 30, Price, "burn-1", "Tender"),
            CancellationToken.None);

        world.Clock.UtcNow = March1.AddDays(11);
        var expired = await world.ExpireDue.ExecuteAsync(new ExpireDueCreditsCommand(), CancellationToken.None);

        expired.IsSuccess.Should().BeTrue();
        expired.Value.Posted.Should().ContainSingle();
        expired.Value.Posted[0].Transaction.Type.Should().Be(LedgerTransactionType.Expire);
        expired.Value.Posted[0].Transaction.Entries.Should().Contain(e => e.Amount == -70);
        (await world.Balance.ExecuteAsync(new GetBalanceQuery(), CancellationToken.None)).Value.Credits.Should().Be(50);
    }

    [Fact]
    public async Task Anonymous_balance_is_not_found()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.Anonymous(world.Maya.PartnerId);

        var result = await world.Balance.ExecuteAsync(new GetBalanceQuery(), CancellationToken.None);

        result.Error.Should().Be(Errors.MemberNotFound);
    }

    private sealed class World
    {
        private World(
            Member maya,
            FakeTenant tenant,
            FakeLedger ledger,
            FakeBookingTenders tenders,
            MutableFakeClock clock,
            EarnCredits earn,
            BurnCredits burn,
            ExpireCredits expire,
            ReverseLedger reverse,
            GetBalance balance,
            GetStatement statement,
            GetLiabilityReport liability,
            ReconcileLedger reconcile,
            ExpireDueCredits expireDue)
        {
            Maya = maya;
            Tenant = tenant;
            Ledger = ledger;
            Tenders = tenders;
            Clock = clock;
            Earn = earn;
            Burn = burn;
            Expire = expire;
            Reverse = reverse;
            Balance = balance;
            Statement = statement;
            Liability = liability;
            Reconcile = reconcile;
            ExpireDue = expireDue;
        }

        public Member Maya { get; }

        public FakeTenant Tenant { get; }

        public FakeLedger Ledger { get; }

        public FakeBookingTenders Tenders { get; }

        public MutableFakeClock Clock { get; }

        public EarnCredits Earn { get; }

        public BurnCredits Burn { get; }

        public ExpireCredits Expire { get; }

        public ReverseLedger Reverse { get; }

        public GetBalance Balance { get; }

        public GetStatement Statement { get; }

        public GetLiabilityReport Liability { get; }

        public ReconcileLedger Reconcile { get; }

        public ExpireDueCredits ExpireDue { get; }

        public static World Summit() => Create(730, March15);

        public static World ShortLived() => Create(10, March1);

        private static World Create(int lifetimeDays, DateTimeOffset start)
        {
            var partner = Partner.Create(
                "SUMMIT",
                "Summit Rewards",
                Currency.Usd,
                new PartnerTheme("#BE185D", "#FFF7ED", "#1D4ED8"),
                new CreditPolicy(0.01m, Percent.From(40m), lifetimeDays, Percent.From(10m)),
                new QuotePolicy(15, RateDriftPolicy.AbsorbWithinTolerance, Percent.From(2m)),
                new SagaPolicy(10, 3, 5, 60),
                new OpportunityPolicy(3, 14, 0.55m, Percent.From(10m), 2, 30, 7, new SignalWeights(0.2m, 0.2m, 0.2m, 0.2m, 0.2m)));
            var maya = Member.Create(partner.Id, "Maya", TierCode.Gold);
            var tenant = new FakeTenant { Current = TenantContext.ForMember(maya) };
            var ledger = new FakeLedger();
            var unitOfWork = new FakeUnitOfWork();
            var clock = new MutableFakeClock(start);
            var claim = new ClaimIdempotency(tenant, new FakeIdempotencyStore(), clock);
            var members = new FakeMembers(maya);
            var partners = new FakePartners(partner);
            var tenders = new FakeBookingTenders();
            var expire = new ExpireCredits(tenant, members, partners, ledger, unitOfWork, claim, clock);

            return new World(
                maya,
                tenant,
                ledger,
                tenders,
                clock,
                new EarnCredits(tenant, members, partners, ledger, unitOfWork, claim, clock),
                new BurnCredits(tenant, members, partners, ledger, unitOfWork, claim, clock),
                expire,
                new ReverseLedger(tenant, members, partners, ledger, unitOfWork, claim, clock),
                new GetBalance(tenant, members, partners, ledger),
                new GetStatement(tenant, members, ledger),
                new GetLiabilityReport(tenant, partners, ledger),
                new ReconcileLedger(tenant, ledger, tenders),
                new ExpireDueCredits(tenant, partners, ledger, expire, clock));
        }
    }
}
