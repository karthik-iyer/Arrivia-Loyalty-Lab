using LoyaltyLab.Application.Booking;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Tests.Booking;

public sealed class ValidateQuoteStepTests
{
    [Fact]
    public async Task Execute_accepts_an_unchanged_rate()
    {
        var world = Harness.Create();
        var step = new ValidateQuoteStep(world.Supplier, world.Clock);

        var outcome = await step.ExecuteAsync(world.Context, CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Succeeded);
        world.Context.Drift!.Kind.Should().Be(RateDriftKind.Unchanged);
    }

    [Fact]
    public async Task Execute_rejects_an_expired_quote()
    {
        var world = Harness.Create();
        world.Clock.UtcNow = world.Quote.ExpiresAt;
        var step = new ValidateQuoteStep(world.Supplier, world.Clock);

        var outcome = await step.ExecuteAsync(world.Context, CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Failed);
        outcome.Error.Should().Be(Errors.QuoteExpired);
    }

    [Fact]
    public async Task Compensate_is_a_no_op()
    {
        var world = Harness.Create();
        var step = new ValidateQuoteStep(world.Supplier, world.Clock);

        var outcome = await step.CompensateAsync(world.Context, CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_unknown_re_runs_validation()
    {
        var world = Harness.Create();
        world.Supplier.NetRate = Result<Money>.Success(Money.Of(150.00m, Currency.Usd));
        var step = new ValidateQuoteStep(world.Supplier, world.Clock);

        var outcome = await step.ResolveUnknownAsync(world.Context, CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Failed);
        outcome.Error.Should().Be(Errors.RateChanged);
    }
}

public sealed class ReserveInventoryStepTests
{
    [Fact]
    public async Task Execute_reserves_with_the_derived_key()
    {
        var world = Harness.Create();
        var step = new ReserveInventoryStep(world.Supplier);

        var outcome = await step.ExecuteAsync(world.Context, CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Succeeded);
        outcome.ExternalReference.Should().Be("res-1");
        world.Context.Key(SagaStepKind.ReserveInventory)
            .Should().Be(SagaInstance.DeriveIdempotencyKey(world.Saga.Id, SagaStepKind.ReserveInventory));
    }

    [Fact]
    public async Task Compensate_releases_the_stored_reference()
    {
        var world = Harness.Create();
        world.Seed(SagaStepKind.ReserveInventory, "res-1");
        var step = new ReserveInventoryStep(world.Supplier);

        var outcome = await step.CompensateAsync(world.Context, CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        world.Supplier.LastReleased.Should().Be("res-1");
    }

    [Fact]
    public async Task Resolve_unknown_queries_by_key()
    {
        var world = Harness.Create();
        world.Supplier.Reserve = StepOutcome.Unknown();
        world.Supplier.Query = StepOutcome.Succeeded("res-1");
        var step = new ReserveInventoryStep(world.Supplier);

        (await step.ExecuteAsync(world.Context, CancellationToken.None)).Result.Should().Be(StepResult.Unknown);
        var resolved = await step.ResolveUnknownAsync(world.Context, CancellationToken.None);

        resolved.Result.Should().Be(StepResult.Succeeded);
        resolved.ExternalReference.Should().Be("res-1");
    }
}

public sealed class AuthorizePaymentStepTests
{
    [Fact]
    public async Task Execute_authorizes_cash_with_the_derived_key()
    {
        var world = Harness.Create();
        var step = new AuthorizePaymentStep(world.Payments);

        var outcome = await step.ExecuteAsync(world.Context, CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Succeeded);
        world.Payments.LastAuthorizeKey.Should().Be(world.Context.Key(SagaStepKind.AuthorizePayment));
    }

    [Fact]
    public async Task Compensate_voids_the_authorization()
    {
        var world = Harness.Create();
        world.Seed(SagaStepKind.AuthorizePayment, "pay-1");
        var step = new AuthorizePaymentStep(world.Payments);

        var outcome = await step.CompensateAsync(world.Context, CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        world.Payments.LastVoidId.Should().Be("pay-1");
    }

    [Fact]
    public async Task Resolve_unknown_queries_the_gateway()
    {
        var world = Harness.Create();
        world.Payments.Authorize = StepOutcome.Unknown();
        world.Payments.Query = StepOutcome.Succeeded("pay-1");
        var step = new AuthorizePaymentStep(world.Payments);

        (await step.ExecuteAsync(world.Context, CancellationToken.None)).Result.Should().Be(StepResult.Unknown);
        var resolved = await step.ResolveUnknownAsync(world.Context, CancellationToken.None);

        resolved.Result.Should().Be(StepResult.Succeeded);
        world.Payments.LastQueryKey.Should().Be(world.Context.Key(SagaStepKind.AuthorizePayment));
    }
}

public sealed class BurnCreditsStepTests
{
    [Fact]
    public async Task Execute_burns_the_tender()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        var step = new BurnCreditsStep(world.Burn, world.Reverse, world.Ledger);

        var outcome = await step.ExecuteAsync(world.Context, CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Succeeded);
        world.Ledger.Transactions.Should().Contain(t => t.Type == LedgerTransactionType.Burn);
    }

    [Fact]
    public async Task Compensate_reverses_the_burn()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        var step = new BurnCreditsStep(world.Burn, world.Reverse, world.Ledger);
        var burned = await step.ExecuteAsync(world.Context, CancellationToken.None);
        world.Seed(SagaStepKind.BurnCredits, burned.ExternalReference);

        var outcome = await step.CompensateAsync(world.Context, CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        world.Ledger.Transactions.Should().Contain(t => t.Type == LedgerTransactionType.Reversal);
    }

    [Fact]
    public async Task Resolve_unknown_replays_the_idempotent_burn()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        var step = new BurnCreditsStep(world.Burn, world.Reverse, world.Ledger);
        var first = await step.ExecuteAsync(world.Context, CancellationToken.None);

        var resolved = await step.ResolveUnknownAsync(world.Context, CancellationToken.None);

        resolved.Result.Should().Be(StepResult.Succeeded);
        resolved.ExternalReference.Should().Be(first.ExternalReference);
        world.Ledger.Transactions.Count(t => t.Type == LedgerTransactionType.Burn).Should().Be(1);
    }
}

public sealed class CapturePaymentStepTests
{
    [Fact]
    public async Task Execute_captures_the_authorized_payment()
    {
        var world = Harness.Create();
        world.Seed(SagaStepKind.AuthorizePayment, "pay-1");
        var step = new CapturePaymentStep(world.Payments);

        var outcome = await step.ExecuteAsync(world.Context, CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Succeeded);
        world.Payments.LastCaptureId.Should().Be("pay-1");
    }

    [Fact]
    public async Task Compensate_refunds_the_capture()
    {
        var world = Harness.Create();
        world.Seed(SagaStepKind.AuthorizePayment, "pay-1");
        world.Seed(SagaStepKind.CapturePayment, "pay-1");
        var step = new CapturePaymentStep(world.Payments);

        var outcome = await step.CompensateAsync(world.Context, CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        world.Payments.LastRefundId.Should().Be("pay-1");
    }

    [Fact]
    public async Task Resolve_unknown_queries_the_capture_key()
    {
        var world = Harness.Create();
        world.Seed(SagaStepKind.AuthorizePayment, "pay-1");
        world.Payments.Capture = StepOutcome.Unknown();
        world.Payments.Query = StepOutcome.Succeeded("pay-1");
        var step = new CapturePaymentStep(world.Payments);

        (await step.ExecuteAsync(world.Context, CancellationToken.None)).Result.Should().Be(StepResult.Unknown);
        var resolved = await step.ResolveUnknownAsync(world.Context, CancellationToken.None);

        resolved.Result.Should().Be(StepResult.Succeeded);
        world.Payments.LastQueryKey.Should().Be(world.Context.Key(SagaStepKind.CapturePayment));
    }
}

public sealed class ConfirmBookingStepTests
{
    [Fact]
    public async Task Execute_confirms_the_booking_and_accrues_earn()
    {
        var world = Harness.Create();
        var step = new ConfirmBookingStep(world.Bookings, world.Earn, world.Reverse);

        var outcome = await step.ExecuteAsync(world.Context, CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Succeeded);
        world.Bookings.Items.Should().ContainSingle(b => b.Status == BookingStatus.Confirmed);
        world.Ledger.Transactions.Should().Contain(t => t.Type == LedgerTransactionType.Earn);
    }

    [Fact]
    public async Task Compensate_cancels_and_reverses_earn()
    {
        var world = Harness.Create();
        var step = new ConfirmBookingStep(world.Bookings, world.Earn, world.Reverse);
        var confirmed = await step.ExecuteAsync(world.Context, CancellationToken.None);
        world.Seed(SagaStepKind.ConfirmBooking, confirmed.ExternalReference);

        var outcome = await step.CompensateAsync(world.Context, CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        world.Bookings.Items[0].Status.Should().Be(BookingStatus.Cancelled);
        world.Ledger.Transactions.Should().Contain(t => t.Type == LedgerTransactionType.Reversal);
    }

    [Fact]
    public async Task Resolve_unknown_replays_confirm_and_earn()
    {
        var world = Harness.Create();
        var step = new ConfirmBookingStep(world.Bookings, world.Earn, world.Reverse);
        var first = await step.ExecuteAsync(world.Context, CancellationToken.None);

        var resolved = await step.ResolveUnknownAsync(world.Context, CancellationToken.None);

        resolved.Result.Should().Be(StepResult.Succeeded);
        resolved.ExternalReference.Should().Be(first.ExternalReference);
        world.Ledger.Transactions.Count(t => t.Type == LedgerTransactionType.Earn).Should().Be(1);
        world.Bookings.Items.Should().ContainSingle();
    }
}

internal sealed class Harness
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Stay = new(2026, 6, 1);

    private Harness(
        SagaContext context,
        SagaInstance saga,
        Quote quote,
        MutableFakeClock clock,
        FakeSupplier supplier,
        FakePayments payments,
        FakeBookings bookings,
        FakeLedger ledger,
        EarnCredits earn,
        BurnCredits burn,
        ReverseLedger reverse)
    {
        Context = context;
        Saga = saga;
        Quote = quote;
        Clock = clock;
        Supplier = supplier;
        Payments = payments;
        Bookings = bookings;
        Ledger = ledger;
        Earn = earn;
        Burn = burn;
        Reverse = reverse;
    }

    public SagaContext Context { get; }

    public SagaInstance Saga { get; }

    public Quote Quote { get; }

    public MutableFakeClock Clock { get; }

    public FakeSupplier Supplier { get; }

    public FakePayments Payments { get; }

    public FakeBookings Bookings { get; }

    public FakeLedger Ledger { get; }

    public EarnCredits Earn { get; }

    public BurnCredits Burn { get; }

    public ReverseLedger Reverse { get; }

    public void Seed(SagaStepKind kind, string? reference)
    {
        Saga.MarkInProgress(kind, Clock);
        if (reference is not null)
        {
            Saga.MarkSucceeded(kind, reference, Clock);
        }
    }

    public Task GrantAsync(int credits) =>
        Earn.ExecuteAsync(new EarnCreditsCommand(Context.Member.Id, credits, "grant", "Test grant"), CancellationToken.None);

    public static Harness Create()
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
        var member = Member.Create(partner.Id, "Maya", TierCode.Gold);
        var offer = TravelOffer.Create(
            SupplierId.New(),
            "Coral Bay Resort",
            new Destination("MBJ", "Montego Bay"),
            Money.Of(100.00m, Currency.Usd),
            Money.Of(15.00m, Currency.Usd),
            [OfferTag.Beach],
            4,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));
        var rules = new PricingRule[]
        {
            BaseMarkupRule.Create(partner.Id, Percent.From(12m), RuleScope.PartnerWide, AsOf),
            TierAdjustmentRule.Create(partner.Id, Percent.From(-3m), new RuleScope(tier: TierCode.Gold), AsOf),
            CampaignDiscountRule.Create(partner.Id, "MARCH-BEACH", Percent.From(-5m), new RuleScope(tag: OfferTag.Beach), AsOf),
            MarginFloorRule.Create(partner.Id, Percent.From(5m), RuleScope.PartnerWide, AsOf),
            BurnCapRule.Create(partner.Id, Percent.From(40m), RuleScope.PartnerWide, AsOf),
        };
        var state = new PricingPipeline().Execute(
            new PricingRequest(
                PricingContext.ForOffer(partner.Id, offer, TierCode.Gold, Stay),
                offer,
                new HashSet<SupplierId> { offer.SupplierId },
                rules,
                AsOf));
        var clock = new MutableFakeClock(AsOf);
        var quote = Quote.Create(member, offer, state, partner.QuotePolicy, clock);
        var saga = SagaInstance.Start(partner.Id, BookingId.New(), "corr-1", clock);
        var tender = new TenderSplit(
            Money.Of(72.45m, Currency.Usd),
            4830,
            Money.Of(48.30m, Currency.Usd));
        var tenant = new FakeTenant { Current = TenantContext.ForMember(member) };
        var ledger = new FakeLedger();
        var unitOfWork = new FakeUnitOfWork();
        var claim = new ClaimIdempotency(tenant, new FakeIdempotencyStore(), clock);
        var members = new FakeMembers(member);
        var partners = new FakePartners(partner);
        var supplier = new FakeSupplier { NetRate = Result<Money>.Success(offer.NetRate) };
        var payments = new FakePayments();
        var bookings = new FakeBookings();
        var earn = new EarnCredits(tenant, members, partners, ledger, unitOfWork, claim, clock);
        var burn = new BurnCredits(tenant, members, partners, ledger, unitOfWork, claim, clock);
        var reverse = new ReverseLedger(tenant, members, partners, ledger, unitOfWork, claim, clock);
        var context = new SagaContext
        {
            Saga = saga,
            Quote = quote,
            Offer = offer,
            Partner = partner,
            Member = member,
            Tender = tender,
            StayDate = Stay,
            FloorAboveNet = Percent.From(5m),
        };

        return new Harness(context, saga, quote, clock, supplier, payments, bookings, ledger, earn, burn, reverse);
    }
}
