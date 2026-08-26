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

public sealed class DetectTravelWindowsTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Maya_has_the_seeded_fourteen_night_window()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        var result = await world.Detect.ExecuteAsync(new DetectTravelWindowsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var window = result.Value.Windows.Should().ContainSingle().Subject;
        window.Start.Should().Be(new DateOnly(2026, 3, 29));
        window.End.Should().Be(new DateOnly(2026, 4, 12));
        window.Nights.Should().Be(14);
    }

    [Fact]
    public async Task Anonymous_detection_does_not_disclose_a_calendar()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.Anonymous(world.Partner.Id);

        var result = await world.Detect.ExecuteAsync(new DetectTravelWindowsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.MemberNotFound);
    }

    private sealed class World
    {
        private World(
            Partner partner,
            Member maya,
            FakeTenant tenant,
            DetectTravelWindows detect)
        {
            Partner = partner;
            Maya = maya;
            Tenant = tenant;
            Detect = detect;
        }

        public Partner Partner { get; }

        public Member Maya { get; }

        public FakeTenant Tenant { get; }

        public DetectTravelWindows Detect { get; }

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
            var tenant = new FakeTenant();
            var busy = new FakeBusyPeriods();
            busy.Add(BusyPeriod.Create(partner.Id, maya.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 29)));
            busy.Add(BusyPeriod.Create(partner.Id, maya.Id, new DateOnly(2026, 4, 12), new DateOnly(2026, 5, 1)));

            var detect = new DetectTravelWindows(
                tenant,
                new FakeClock(AsOf),
                new FakeMembers(maya),
                new FakePartners(partner),
                busy);

            return new World(partner, maya, tenant, detect);
        }
    }
}

public sealed class EvaluateOpportunitiesTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Maya_receives_a_delivered_nudge_priced_through_the_engine()
    {
        var world = World.Summit(withHistory: true);
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        var result = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var nudge = result.Value.Nudges.Should().ContainSingle().Subject;
        nudge.Status.Should().Be(NudgeStatus.Delivered);
        nudge.OfferId.Should().Be(world.Coral.Id);
        nudge.WindowStart.Should().Be(new DateOnly(2026, 3, 29));
        nudge.WindowEnd.Should().Be(new DateOnly(2026, 4, 12));
        nudge.Score.Should().Be(0.68m);
        nudge.Score.Should().Be(nudge.Signals.Sum(signal => signal.Contribution));
        nudge.Signals.Should().HaveCount(5);
        nudge.Signals.Single(signal => signal.Kind == SignalKind.CreditCoverage).Normalized.Should().Be(0.4m);
        world.Quotes.Items.Should().HaveCount(3);
        world.UnitOfWork.Saves.Should().Be(1);
        world.Nudges.Items.Should().ContainSingle(item => item.Status == NudgeStatus.Delivered);
    }

    [Fact]
    public async Task Without_booking_history_the_score_is_recorded_as_below_threshold()
    {
        var world = World.Summit(withHistory: false);
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        var result = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var nudge = result.Value.Nudges.Should().ContainSingle().Subject;
        nudge.Status.Should().Be(NudgeStatus.Suppressed);
        nudge.SuppressedBecause.Should().Be(SuppressionReason.ScoreBelowThreshold);
        nudge.Signals.Should().HaveCount(5);
        nudge.Score.Should().Be(0.28m);
        world.Quotes.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task No_permitted_inventory_records_no_eligible_inventory()
    {
        var world = World.Summit(withHistory: true, permitCoral: false);
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        var result = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var nudge = result.Value.Nudges.Should().ContainSingle().Subject;
        nudge.Status.Should().Be(NudgeStatus.Suppressed);
        nudge.SuppressedBecause.Should().Be(SuppressionReason.NoEligibleInventory);
        nudge.OfferId.Should().BeNull();
    }

    [Fact]
    public async Task A_member_with_no_qualifying_window_is_recorded_as_too_soon()
    {
        var world = World.Summit(withHistory: false, includeBusy: false);
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        var result = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var nudge = result.Value.Nudges.Should().ContainSingle().Subject;
        nudge.Status.Should().Be(NudgeStatus.Suppressed);
        nudge.SuppressedBecause.Should().Be(SuppressionReason.WindowTooSoon);
    }

    [Fact]
    public async Task Anonymous_evaluation_does_not_write_a_nudge()
    {
        var world = World.Summit(withHistory: true);
        world.Tenant.Current = TenantContext.Anonymous(world.Partner.Id);

        var result = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.MemberNotFound);
        world.Nudges.Items.Should().BeEmpty();
        world.UnitOfWork.Saves.Should().Be(0);
    }

    [Fact]
    public async Task A_nudge_appears_with_its_reasoning_and_a_second_is_suppressed_with_a_recorded_reason()
    {
        var world = World.Summit(withHistory: true);
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        var first = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);
        var second = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        var delivered = first.Value.Nudges.Should().ContainSingle().Subject;
        delivered.Status.Should().Be(NudgeStatus.Delivered);
        delivered.Signals.Should().HaveCount(5);
        delivered.Score.Should().Be(delivered.Signals.Sum(signal => signal.Contribution));

        var silenced = second.Value.Nudges.Should().ContainSingle().Subject;
        silenced.Status.Should().Be(NudgeStatus.Suppressed);
        silenced.SuppressedBecause.Should().Be(SuppressionReason.DuplicateOfRecentNudge);
        world.Nudges.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Raising_the_score_threshold_turns_a_deliverable_nudge_into_a_recorded_silence()
    {
        var world = World.Summit(withHistory: true, policy: World.Opportunities(scoreThreshold: 0.90m));
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        var result = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var nudge = result.Value.Nudges.Should().ContainSingle().Subject;
        nudge.Status.Should().Be(NudgeStatus.Suppressed);
        nudge.SuppressedBecause.Should().Be(SuppressionReason.ScoreBelowThreshold);
        nudge.Score.Should().Be(0.68m);
        nudge.Signals.Should().HaveCount(5);
    }

    [Fact]
    public async Task Raising_the_weekly_cap_allows_a_delivery_that_the_default_cap_would_block()
    {
        var world = World.Summit(withHistory: true, policy: World.Opportunities(maxNudgesPerMemberPerWeek: 3));
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var other = new TravelWindow(world.Maya.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10));
        world.Nudges.Items.Add(Prior(world, OfferId.New(), other));
        world.Nudges.Items.Add(Prior(world, OfferId.New(), other));

        var result = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var nudge = result.Value.Nudges.Should().ContainSingle().Subject;
        nudge.Status.Should().Be(NudgeStatus.Delivered);
        nudge.OfferId.Should().Be(world.Coral.Id);
    }

    [Fact]
    public async Task A_second_scan_records_a_duplicate_suppression()
    {
        var world = World.Summit(withHistory: true);
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);
        var second = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        var nudge = second.Value.Nudges.Should().ContainSingle().Subject;
        nudge.Status.Should().Be(NudgeStatus.Suppressed);
        nudge.SuppressedBecause.Should().Be(SuppressionReason.DuplicateOfRecentNudge);
        world.Nudges.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_weekly_cap_is_recorded_rather_than_a_third_delivery()
    {
        var world = World.Summit(withHistory: true);
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var other = new TravelWindow(world.Maya.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10));
        world.Nudges.Items.Add(Prior(world, OfferId.New(), other));
        world.Nudges.Items.Add(Prior(world, OfferId.New(), other));

        var result = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var nudge = result.Value.Nudges.Should().ContainSingle().Subject;
        nudge.Status.Should().Be(NudgeStatus.Suppressed);
        nudge.SuppressedBecause.Should().Be(SuppressionReason.FatigueCapReached);
    }

    [Fact]
    public async Task Dismissing_a_nudge_records_cooldown_on_the_next_scan()
    {
        var world = World.Summit(withHistory: true);
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);
        world.Nudges.Items[0].Dismiss();
        var second = await world.Evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        var nudge = second.Value.Nudges.Should().ContainSingle().Subject;
        nudge.Status.Should().Be(NudgeStatus.Suppressed);
        nudge.SuppressedBecause.Should().Be(SuppressionReason.CooldownActive);
    }

    private static Nudge Prior(World world, OfferId offer, TravelWindow window) =>
        Nudge.Deliver(
            world.Partner.Id,
            world.Maya.Id,
            offer,
            window,
            [OpportunitySignal.Of(SignalKind.WindowFit, 14m, 1m, 1m)],
            world.Partner.OpportunityPolicy,
            new FakeClock(AsOf));

    private sealed class World
    {
        private World(
            Partner partner,
            Member maya,
            TravelOffer coral,
            FakeTenant tenant,
            FakeQuotes quotes,
            FakeNudges nudges,
            FakeUnitOfWork unitOfWork,
            EvaluateOpportunities evaluate)
        {
            Partner = partner;
            Maya = maya;
            Coral = coral;
            Tenant = tenant;
            Quotes = quotes;
            Nudges = nudges;
            UnitOfWork = unitOfWork;
            Evaluate = evaluate;
        }

        public Partner Partner { get; }

        public Member Maya { get; }

        public TravelOffer Coral { get; }

        public FakeTenant Tenant { get; }

        public FakeQuotes Quotes { get; }

        public FakeNudges Nudges { get; }

        public FakeUnitOfWork UnitOfWork { get; }

        public EvaluateOpportunities Evaluate { get; }

        public static OpportunityPolicy Opportunities(
            decimal scoreThreshold = 0.55m,
            int maxNudgesPerMemberPerWeek = 2) =>
            new(
                3,
                14,
                scoreThreshold,
                Percent.From(10m),
                maxNudgesPerMemberPerWeek,
                30,
                7,
                new SignalWeights(0.2m, 0.2m, 0.2m, 0.2m, 0.2m));

        public static World Summit(
            bool withHistory,
            bool permitCoral = true,
            bool includeBusy = true,
            OpportunityPolicy? policy = null)
        {
            var partner = Partner.Create(
                "SUMMIT",
                "Summit Rewards",
                Currency.Usd,
                new PartnerTheme("#BE185D", "#FFF7ED", "#1D4ED8"),
                new CreditPolicy(0.01m, Percent.From(40m), 730, Percent.From(10m)),
                new QuotePolicy(15, RateDriftPolicy.AbsorbWithinTolerance, Percent.From(2m)),
                new SagaPolicy(10, 3, 5, 60),
                policy ?? Opportunities());
            var maya = Member.Create(partner.Id, "Maya", TierCode.Gold);
            var coral = Offer("Coral Bay Resort", "MBJ", "Montego Bay", [OfferTag.Beach, OfferTag.Family], 100m, 15m);
            var alpine = Offer("Matterhorn Lodge", "ZRH", "Zermatt", [OfferTag.Ski], 180m, 22m);

            var tenant = new FakeTenant();
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
            if (permitCoral)
            {
                permits.Allow(partner.Id, coral.SupplierId, alpine.SupplierId);
            }

            var clock = new FakeClock(AsOf);
            var offers = new FakeOffers(coral, alpine);
            var members = new FakeMembers(maya);
            var partners = new FakePartners(partner);
            var ledger = SeedLedger(partner, maya, clock);
            var nudges = new FakeNudges();

            if (withHistory)
            {
                SeedStays(maya, partner, coral, quotes, bookings, clock);
            }

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
                new FakePriceWatches(),
                nudges,
                getBalance,
                unitOfWork);

            return new World(partner, maya, coral, tenant, quotes, nudges, unitOfWork, evaluate);
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
