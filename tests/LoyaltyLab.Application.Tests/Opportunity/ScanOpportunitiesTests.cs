using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Application.Opportunity;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Opportunity;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Tests.Opportunity;

public sealed class ScanOpportunitiesTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Scan_evaluates_maya_against_the_seeded_drop_then_rolls_the_baseline()
    {
        var world = World.Summit();
        world.Supplier.Rates[world.Coral.Id] = Result<Money>.Success(world.Coral.NetRate);

        var result = await world.Scan.ExecuteAsync(new ScanOpportunitiesCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MembersScanned.Should().Be(1);
        result.Value.WatchesRefreshed.Should().Be(1);
        var nudge = world.Nudges.Items.Should().ContainSingle(item => item.Status == NudgeStatus.Delivered).Subject;
        nudge.OfferId.Should().Be(world.Coral.Id);
        nudge.Signals.Single(signal => signal.Kind == SignalKind.PriceDrop).Normalized.Should().BeGreaterThan(0m);
        world.Watches.Items.Single(watch => watch.OfferId == world.Coral.Id).BaselineNetRate.Should().Be(world.Coral.NetRate);
        world.Supplier.RateCalls.Should().Be(1);
    }

    [Fact]
    public async Task Refresh_is_bounded_to_the_stalest_batch()
    {
        var world = World.Summit();
        world.Supplier.NetRate = Result<Money>.Success(Money.Of(90m, Currency.Usd));

        var result = await world.Scan.ExecuteAsync(new ScanOpportunitiesCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.WatchesRefreshed.Should().Be(1);
        world.Watches.Items.Single(watch => watch.OfferId == world.Coral.Id).BaselineNetRate.Amount.Should().Be(90m);
        world.Watches.Items.Where(watch => watch.OfferId != world.Coral.Id)
            .Should()
            .ContainSingle()
            .Which.BaselineNetRate.Amount.Should().Be(180m);
        world.Supplier.RateCalls.Should().Be(1);
    }

    [Fact]
    public async Task Members_without_busy_periods_are_not_scanned()
    {
        var world = World.Summit(includeBusy: false);

        var result = await world.Scan.ExecuteAsync(new ScanOpportunitiesCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MembersScanned.Should().Be(0);
        world.Nudges.Items.Should().BeEmpty();
    }

    private sealed class World
    {
        private World(
            TravelOffer coral,
            FakeNudges nudges,
            FakePriceWatches watches,
            FakeSupplier supplier,
            ScanOpportunities scan)
        {
            Coral = coral;
            Nudges = nudges;
            Watches = watches;
            Supplier = supplier;
            Scan = scan;
        }

        public TravelOffer Coral { get; }

        public FakeNudges Nudges { get; }

        public FakePriceWatches Watches { get; }

        public FakeSupplier Supplier { get; }

        public ScanOpportunities Scan { get; }

        public static World Summit(bool includeBusy = true)
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
            var coral = Offer("Coral Bay Resort", "MBJ", "Montego Bay", [OfferTag.Beach, OfferTag.Family], 100m, 15m);
            var alpine = Offer("Matterhorn Lodge", "ZRH", "Zermatt", [OfferTag.Ski], 180m, 22m);

            var tenant = new FakeTenant { Current = TenantContext.Anonymous(partner.Id) };
            var unitOfWork = new FakeUnitOfWork();
            var quotes = new FakeQuotes(tenant);
            var bookings = new FakeBookings();
            var busy = new FakeBusyPeriods();
            if (includeBusy)
            {
                busy.Add(BusyPeriod.Create(partner.Id, maya.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 29)));
                busy.Add(BusyPeriod.Create(partner.Id, maya.Id, new DateOnly(2026, 4, 12), new DateOnly(2026, 5, 1)));
            }

            var permits = new FakePermits();
            permits.Allow(partner.Id, coral.SupplierId, alpine.SupplierId);
            var clock = new FakeClock(AsOf);
            var offers = new FakeOffers(coral, alpine);
            var members = new FakeMembers(maya);
            var partners = new FakePartners(partner);
            var ledger = SeedLedger(partner, maya, clock);
            var nudges = new FakeNudges();
            var watches = new FakePriceWatches();
            watches.Add(
                PriceWatch.Open(
                    partner.Id,
                    coral.Id,
                    Money.Of(115m, Currency.Usd),
                    new FakeClock(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero))));
            SeedStays(maya, partner, coral, quotes, bookings, clock);

            var getBalance = new GetBalance(tenant, members, partners, ledger);
            var evaluate = new EvaluateOpportunities(
                tenant,
                clock,
                members,
                partners,
                busy,
                offers,
                permits,
                new FakeRules(SummitRules(partner.Id)),
                bookings,
                quotes,
                watches,
                nudges,
                getBalance,
                unitOfWork);
            var supplier = new FakeSupplier();
            var scan = new ScanOpportunities(
                tenant,
                clock,
                partners,
                members,
                offers,
                permits,
                busy,
                watches,
                supplier,
                evaluate,
                unitOfWork);

            return new World(coral, nudges, watches, supplier, scan);
        }

        private static void SeedStays(
            Member maya,
            Partner partner,
            TravelOffer coral,
            FakeQuotes quotes,
            FakeBookings bookings,
            FakeClock clock)
        {
            var pipeline = new PricingPipeline();
            var permitted = (IReadOnlySet<SupplierId>)new HashSet<SupplierId> { coral.SupplierId };
            var rules = SummitRules(partner.Id);
            for (var i = 0; i < 3; i++)
            {
                var stay = new DateOnly(2026, 1, 8).AddDays(i * 14);
                var priced = pipeline.Execute(
                    new PricingRequest(
                        PricingContext.ForOffer(partner.Id, coral, maya.Tier, stay),
                        coral,
                        permitted,
                        rules,
                        clock.UtcNow));
                var quote = Quote.Create(maya, coral, priced, partner.QuotePolicy, clock);
                quotes.AddAsync(quote, CancellationToken.None).GetAwaiter().GetResult();
                var booking = global::LoyaltyLab.Domain.Booking.Booking.Place(
                    BookingId.New(),
                    partner.Id,
                    maya.Id,
                    quote.Id,
                    new TenderSplit(priced.RunningTotal, 0, Money.Zero(Currency.Usd)));
                booking.Confirm("seed", RateDriftOutcome.Unchanged);
                bookings.AddAsync(booking, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        private static FakeLedger SeedLedger(Partner partner, Member member, FakeClock clock)
        {
            var ledger = new FakeLedger();
            var issuance = LedgerAccount.Issuance(partner.Id);
            var credits = LedgerAccount.MemberCredits(partner.Id, member.Id);
            ledger.AddAccountAsync(issuance, CancellationToken.None).GetAwaiter().GetResult();
            ledger.AddAccountAsync(credits, CancellationToken.None).GetAwaiter().GetResult();
            ledger.AddAsync(
                    LedgerTransaction.Earn(credits, issuance, 6_000, "seed-earn", "Opening grant", clock),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return ledger;
        }

        private static PricingRule[] SummitRules(PartnerId partner) =>
        [
            BaseMarkupRule.Create(partner, Percent.From(12m), RuleScope.PartnerWide, AsOf),
            TierAdjustmentRule.Create(partner, Percent.From(-3m), new RuleScope(tier: TierCode.Gold), AsOf),
            CampaignDiscountRule.Create(partner, "MARCH-BEACH", Percent.From(-5m), new RuleScope(tag: OfferTag.Beach), AsOf),
            MarginFloorRule.Create(partner, Percent.From(5m), RuleScope.PartnerWide, AsOf),
            BurnCapRule.Create(partner, Percent.From(40m), RuleScope.PartnerWide, AsOf),
        ];

        private static TravelOffer Offer(
            string name,
            string destinationCode,
            string destinationName,
            OfferTag[] tags,
            decimal net,
            decimal tax) =>
            TravelOffer.Create(
                SupplierId.New(),
                name,
                new Destination(destinationCode, destinationName),
                Money.Of(net, Currency.Usd),
                Money.Of(tax, Currency.Usd),
                tags,
                4,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 6, 30));
    }
}
