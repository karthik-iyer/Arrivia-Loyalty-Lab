using LoyaltyLab.Application.Opportunity;
using LoyaltyLab.Application.Pricing;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Tests.Opportunity;

public sealed class InboxUseCaseTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly WindowStart = new(2026, 3, 29);
    private static readonly DateOnly WindowEnd = new(2026, 4, 12);

    [Fact]
    public async Task Inbox_lists_only_live_delivered_nudges()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var live = world.DeliverCoral();
        world.DeliverCoral().Dismiss();

        var result = await world.Inbox.ExecuteAsync(new GetInboxQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Nudges.Should().ContainSingle(nudge => nudge.Id == live.Id);
        world.UnitOfWork.Saves.Should().Be(0);
    }

    [Fact]
    public async Task Inbox_omits_expired_nudges_and_stamps_them()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var nudge = world.DeliverCoral();
        var inbox = world.InboxAt(AsOf.AddDays(7));

        var result = await inbox.ExecuteAsync(new GetInboxQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Nudges.Should().BeEmpty();
        nudge.Status.Should().Be(NudgeStatus.Expired);
        world.UnitOfWork.Saves.Should().Be(1);
    }

    [Fact]
    public async Task Actioning_re_quotes_through_the_engine()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var nudge = world.DeliverCoral();

        var result = await world.Action.ExecuteAsync(new ActionNudgeCommand(nudge.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NudgeId.Should().Be(nudge.Id);
        result.Value.OfferId.Should().Be(world.Coral.Id);
        result.Value.MemberPrice.Amount.Should().Be(120.75m);
        result.Value.MaxCredits.Should().Be(4830);
        nudge.Status.Should().Be(NudgeStatus.Actioned);
        world.Quotes.Items.Should().ContainSingle(quote => quote.Id == result.Value.QuoteId);
        world.UnitOfWork.Saves.Should().Be(2);
        typeof(Nudge).GetProperty("MemberPrice").Should().BeNull();
    }

    [Fact]
    public async Task Actioning_an_expired_nudge_is_gone()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var nudge = world.DeliverCoral();
        var action = world.ActionAt(AsOf.AddDays(7));

        var result = await action.ExecuteAsync(new ActionNudgeCommand(nudge.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.NudgeExpired);
        nudge.Status.Should().Be(NudgeStatus.Expired);
        world.Quotes.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failed_quote_leaves_the_nudge_delivered()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var nudge = world.Deliver(OfferId.New());

        var result = await world.Action.ExecuteAsync(new ActionNudgeCommand(nudge.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.OfferNotFound);
        nudge.Status.Should().Be(NudgeStatus.Delivered);
        world.Quotes.Items.Should().BeEmpty();
        world.UnitOfWork.Saves.Should().Be(0);
    }

    [Fact]
    public async Task Dismissing_a_nudge_removes_it_from_the_inbox()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var nudge = world.DeliverCoral();

        var dismissed = await world.Dismiss.ExecuteAsync(new DismissNudgeCommand(nudge.Id), CancellationToken.None);
        var inbox = await world.Inbox.ExecuteAsync(new GetInboxQuery(), CancellationToken.None);

        dismissed.IsSuccess.Should().BeTrue();
        dismissed.Value.Status.Should().Be(NudgeStatus.Dismissed);
        nudge.Status.Should().Be(NudgeStatus.Dismissed);
        inbox.Value.Nudges.Should().BeEmpty();
        world.UnitOfWork.Saves.Should().Be(1);
    }

    [Fact]
    public async Task Another_member_cannot_action_the_nudge()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var nudge = world.DeliverCoral();
        world.Tenant.Current = TenantContext.ForMember(world.Ravi);

        var result = await world.Action.ExecuteAsync(new ActionNudgeCommand(nudge.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.NudgeNotFound);
        nudge.Status.Should().Be(NudgeStatus.Delivered);
    }

    [Fact]
    public async Task Anonymous_inbox_is_not_found()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.Anonymous(world.Partner.Id);

        var result = await world.Inbox.ExecuteAsync(new GetInboxQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.MemberNotFound);
    }

    private sealed class World
    {
        private World(
            Partner partner,
            Member maya,
            Member ravi,
            TravelOffer coral,
            FakeTenant tenant,
            FakeQuotes quotes,
            FakeNudges nudges,
            FakeUnitOfWork unitOfWork,
            GetInbox inbox,
            ActionNudge action,
            DismissNudge dismiss)
        {
            Partner = partner;
            Maya = maya;
            Ravi = ravi;
            Coral = coral;
            Tenant = tenant;
            Quotes = quotes;
            Nudges = nudges;
            UnitOfWork = unitOfWork;
            Inbox = inbox;
            Action = action;
            Dismiss = dismiss;
        }

        public Partner Partner { get; }

        public Member Maya { get; }

        public Member Ravi { get; }

        public TravelOffer Coral { get; }

        public FakeTenant Tenant { get; }

        public FakeQuotes Quotes { get; }

        public FakeNudges Nudges { get; }

        public FakeUnitOfWork UnitOfWork { get; }

        public GetInbox Inbox { get; }

        public ActionNudge Action { get; }

        public DismissNudge Dismiss { get; }

        public GetInbox InboxAt(DateTimeOffset when) =>
            new(Tenant, new FakeClock(when), Nudges, UnitOfWork);

        public ActionNudge ActionAt(DateTimeOffset when)
        {
            var clock = new FakeClock(when);
            var quote = QuoteAt(clock);
            return new ActionNudge(Tenant, clock, Nudges, quote, UnitOfWork);
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
            var ravi = Member.Create(partner.Id, "Ravi", TierCode.Standard);
            var coral = TravelOffer.Create(
                SupplierId.New(),
                "Coral Bay Resort",
                new Destination("MBJ", "Montego Bay"),
                Money.Of(100.00m, Currency.Usd),
                Money.Of(15.00m, Currency.Usd),
                [OfferTag.Beach],
                4,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 6, 30));

            var tenant = new FakeTenant();
            var unitOfWork = new FakeUnitOfWork();
            var quotes = new FakeQuotes(tenant);
            var nudges = new FakeNudges();
            var clock = new FakeClock(AsOf);
            var permits = new FakePermits();
            permits.Allow(partner.Id, coral.SupplierId);
            var rules = new FakeRules(
                BaseMarkupRule.Create(partner.Id, Percent.From(12m), RuleScope.PartnerWide, AsOf),
                TierAdjustmentRule.Create(partner.Id, Percent.From(-3m), new RuleScope(tier: TierCode.Gold), AsOf),
                CampaignDiscountRule.Create(partner.Id, "MARCH-BEACH", Percent.From(-5m), new RuleScope(tag: OfferTag.Beach), AsOf),
                MarginFloorRule.Create(partner.Id, Percent.From(5m), RuleScope.PartnerWide, AsOf),
                BurnCapRule.Create(partner.Id, Percent.From(40m), RuleScope.PartnerWide, AsOf));
            var quote = new QuoteOffer(
                tenant,
                new FakeOffers(coral),
                new FakeMembers(maya, ravi),
                new FakePartners(partner),
                rules,
                permits,
                quotes,
                unitOfWork,
                clock);
            var inbox = new GetInbox(tenant, clock, nudges, unitOfWork);
            var action = new ActionNudge(tenant, clock, nudges, quote, unitOfWork);
            var dismiss = new DismissNudge(tenant, clock, nudges, unitOfWork);

            return new World(partner, maya, ravi, coral, tenant, quotes, nudges, unitOfWork, inbox, action, dismiss);
        }

        public Nudge DeliverCoral() => Deliver(Coral.Id);

        public Nudge Deliver(OfferId offerId)
        {
            var window = new TravelWindow(Maya.Id, WindowStart, WindowEnd);
            var nudge = Nudge.Deliver(
                Partner.Id,
                Maya.Id,
                offerId,
                window,
                [OpportunitySignal.Of(SignalKind.WindowFit, 14m, 1m, 1m)],
                Partner.OpportunityPolicy,
                new FakeClock(AsOf));
            Nudges.Items.Add(nudge);
            return nudge;
        }

        private QuoteOffer QuoteAt(IClock clock) =>
            new(
                Tenant,
                new FakeOffers(Coral),
                new FakeMembers(Maya, Ravi),
                new FakePartners(Partner),
                new FakeRules(
                    BaseMarkupRule.Create(Partner.Id, Percent.From(12m), RuleScope.PartnerWide, AsOf),
                    TierAdjustmentRule.Create(Partner.Id, Percent.From(-3m), new RuleScope(tier: TierCode.Gold), AsOf),
                    CampaignDiscountRule.Create(Partner.Id, "MARCH-BEACH", Percent.From(-5m), new RuleScope(tag: OfferTag.Beach), AsOf),
                    MarginFloorRule.Create(Partner.Id, Percent.From(5m), RuleScope.PartnerWide, AsOf),
                    BurnCapRule.Create(Partner.Id, Percent.From(40m), RuleScope.PartnerWide, AsOf)),
                Permits(),
                Quotes,
                UnitOfWork,
                clock);

        private FakePermits Permits()
        {
            var permits = new FakePermits();
            permits.Allow(Partner.Id, Coral.SupplierId);
            return permits;
        }
    }
}
