using LoyaltyLab.Application.Pricing;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Tests;

public sealed class PricingUseCaseTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Stay = new(2026, 3, 15);

    [Fact]
    public async Task Search_for_anonymous_lists_inventory_without_a_member_price()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.Anonymous(world.Partner.Id);

        var result = await world.Search.ExecuteAsync(new SearchOffersQuery(Stay), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(o => o.OfferId == world.Oceanic.Id);
        result.Value[0].MemberPrice.Should().BeNull();
        typeof(OfferSummary).GetProperty("NetRate").Should().BeNull();
        typeof(OfferSummary).GetProperty("NetCost").Should().BeNull();
    }

    [Fact]
    public async Task Search_for_a_member_includes_the_partner_price()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        var result = await world.Search.ExecuteAsync(new SearchOffersQuery(Stay), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(o =>
            o.OfferId == world.Oceanic.Id && o.MemberPrice == Money.Of(120.75m, Currency.Usd));
    }

    [Fact]
    public async Task Search_hides_suppliers_the_partner_may_not_sell()
    {
        var world = World.Nimbus();
        world.Tenant.Current = TenantContext.Anonymous(world.Partner.Id);

        var result = await world.Search.ExecuteAsync(new SearchOffersQuery(Stay), CancellationToken.None);

        result.Value.Should().NotContain(o => o.OfferId == world.Oceanic.Id);
        result.Value.Should().ContainSingle(o => o.OfferId == world.Alpine.Id);
    }

    [Fact]
    public async Task Quote_persists_a_priced_snapshot()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        var result = await world.Quote.ExecuteAsync(new QuoteOfferCommand(world.Oceanic.Id, Stay), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MemberPrice.Amount.Should().Be(120.75m);
        result.Value.MaxCredits.Should().Be(4830);
        world.UnitOfWork.Saves.Should().Be(1);
        typeof(QuoteResult).GetProperty("NetRate").Should().BeNull();
    }

    [Fact]
    public async Task Quote_of_an_unknown_offer_is_not_found()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);

        var result = await world.Quote.ExecuteAsync(new QuoteOfferCommand(OfferId.New(), Stay), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.OfferNotFound);
        world.UnitOfWork.Saves.Should().Be(0);
    }

    [Fact]
    public async Task Quote_without_a_member_does_not_disclose_the_offer()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.Anonymous(world.Partner.Id);

        var result = await world.Quote.ExecuteAsync(new QuoteOfferCommand(world.Oceanic.Id, Stay), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.OfferNotFound);
        world.UnitOfWork.Saves.Should().Be(0);
    }

    [Fact]
    public async Task Quote_of_a_forbidden_supplier_is_not_eligible()
    {
        var world = World.Nimbus();
        world.Tenant.Current = TenantContext.ForMember(world.Chen);

        var result = await world.Quote.ExecuteAsync(new QuoteOfferCommand(world.Oceanic.Id, Stay), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.OfferNotEligible);
    }

    [Fact]
    public async Task Explain_for_a_member_does_not_reveal_net_rate()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var quoted = await world.Quote.ExecuteAsync(new QuoteOfferCommand(world.Oceanic.Id, Stay), CancellationToken.None);

        var explained = await world.Explain.ExecuteAsync(new ExplainQuoteQuery(quoted.Value.QuoteId), CancellationToken.None);

        explained.IsSuccess.Should().BeTrue();
        explained.Value.RevealsNetRate.Should().BeFalse();
        explained.Value.NetCost.Should().BeNull();
        explained.Value.MemberPrice.Amount.Should().Be(120.75m);
    }

    [Fact]
    public async Task Explain_for_an_internal_role_includes_net_cost()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var quoted = await world.Quote.ExecuteAsync(new QuoteOfferCommand(world.Oceanic.Id, Stay), CancellationToken.None);
        world.Tenant.Current = new TenantContext(world.Partner.Id, memberId: null, tier: null, AccessRole.AccountManager);

        var explained = await world.Explain.ExecuteAsync(new ExplainQuoteQuery(quoted.Value.QuoteId), CancellationToken.None);

        explained.Value.NetCost!.Value.Amount.Should().Be(115.00m);
        explained.Value.Margin!.Value.Amount.Should().Be(5.75m);
    }

    [Fact]
    public async Task Explain_of_another_members_quote_is_not_found()
    {
        var world = World.Summit();
        world.Tenant.Current = TenantContext.ForMember(world.Maya);
        var quoted = await world.Quote.ExecuteAsync(new QuoteOfferCommand(world.Oceanic.Id, Stay), CancellationToken.None);
        world.Tenant.Current = TenantContext.ForMember(world.Ravi);

        var explained = await world.Explain.ExecuteAsync(new ExplainQuoteQuery(quoted.Value.QuoteId), CancellationToken.None);

        explained.IsFailure.Should().BeTrue();
        explained.Error.Should().Be(Errors.QuoteNotFound);
    }

    private sealed class World
    {
        private World(
            Partner partner,
            Member maya,
            Member ravi,
            Member chen,
            TravelOffer oceanic,
            TravelOffer alpine,
            FakeTenant tenant,
            FakeUnitOfWork unitOfWork,
            SearchOffers search,
            QuoteOffer quote,
            ExplainQuote explain)
        {
            Partner = partner;
            Maya = maya;
            Ravi = ravi;
            Chen = chen;
            Oceanic = oceanic;
            Alpine = alpine;
            Tenant = tenant;
            UnitOfWork = unitOfWork;
            Search = search;
            Quote = quote;
            Explain = explain;
        }

        public Partner Partner { get; }

        public Member Maya { get; }

        public Member Ravi { get; }

        public Member Chen { get; }

        public TravelOffer Oceanic { get; }

        public TravelOffer Alpine { get; }

        public FakeTenant Tenant { get; }

        public FakeUnitOfWork UnitOfWork { get; }

        public SearchOffers Search { get; }

        public QuoteOffer Quote { get; }

        public ExplainQuote Explain { get; }

        public static World Summit() => Create("SUMMIT", Percent.From(12m), Percent.From(40m), RateDriftPolicy.AbsorbWithinTolerance, includeOceanic: true);

        public static World Nimbus() => Create("NIMBUS", Percent.From(18m), Percent.From(100m), RateDriftPolicy.RequoteRequired, includeOceanic: false);

        private static World Create(
            string code,
            Percent markup,
            Percent burnCap,
            RateDriftPolicy drift,
            bool includeOceanic)
        {
            var partner = Partner.Create(
                code,
                code,
                Currency.Usd,
                new PartnerTheme("#BE185D", "#FFF7ED", "#1D4ED8"),
                new CreditPolicy(0.01m, burnCap, 730, Percent.From(10m)),
                new QuotePolicy(15, drift, Percent.From(2m)),
                new SagaPolicy(10, 3, 5, 60),
                new OpportunityPolicy(3, 14, 0.55m, Percent.From(10m), 2, 30, 7, new SignalWeights(0.2m, 0.2m, 0.2m, 0.2m, 0.2m)));

            var maya = Member.Create(partner.Id, "Maya", TierCode.Gold);
            var ravi = Member.Create(partner.Id, "Ravi", TierCode.Standard);
            var chen = Member.Create(partner.Id, "Chen", TierCode.Standard);
            var oceanic = Beach("Coral Bay Resort");
            var alpine = Ski("Matterhorn Lodge");

            var tenant = new FakeTenant();
            var unitOfWork = new FakeUnitOfWork();
            var quotes = new FakeQuotes(tenant);
            var permits = new FakePermits();
            permits.Allow(partner.Id, includeOceanic ? [oceanic.SupplierId, alpine.SupplierId] : [alpine.SupplierId]);

            PricingRule[] rules = includeOceanic
                ? SummitRules(partner.Id, markup, burnCap)
                : NimbusRules(partner.Id, markup, burnCap);
            var offers = new FakeOffers(oceanic, alpine);
            var members = new FakeMembers(maya, ravi, chen);
            var partners = new FakePartners(partner);
            var clock = new FakeClock(AsOf);

            return new World(
                partner,
                maya,
                ravi,
                chen,
                oceanic,
                alpine,
                tenant,
                unitOfWork,
                new SearchOffers(tenant, offers, new FakeRules(rules), permits, clock),
                new QuoteOffer(tenant, offers, members, partners, new FakeRules(rules), permits, quotes, unitOfWork, clock),
                new ExplainQuote(tenant, quotes));
        }

        private static PricingRule[] SummitRules(PartnerId partner, Percent markup, Percent burnCap) =>
        [
            BaseMarkupRule.Create(partner, markup, RuleScope.PartnerWide, AsOf),
            TierAdjustmentRule.Create(partner, Percent.From(-3m), new RuleScope(tier: TierCode.Gold), AsOf),
            CampaignDiscountRule.Create(partner, "MARCH-BEACH", Percent.From(-5m), new RuleScope(tag: OfferTag.Beach), AsOf),
            MarginFloorRule.Create(partner, Percent.From(5m), RuleScope.PartnerWide, AsOf),
            BurnCapRule.Create(partner, burnCap, RuleScope.PartnerWide, AsOf),
        ];

        private static PricingRule[] NimbusRules(PartnerId partner, Percent markup, Percent burnCap) =>
        [
            BaseMarkupRule.Create(partner, markup, RuleScope.PartnerWide, AsOf),
            MarginFloorRule.Create(partner, Percent.From(5m), RuleScope.PartnerWide, AsOf),
            BurnCapRule.Create(partner, burnCap, RuleScope.PartnerWide, AsOf),
        ];

        private static TravelOffer Beach(string name) =>
            TravelOffer.Create(
                SupplierId.New(),
                name,
                new Destination("MBJ", "Montego Bay"),
                Money.Of(100.00m, Currency.Usd),
                Money.Of(15.00m, Currency.Usd),
                [OfferTag.Beach],
                4,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 6, 30));

        private static TravelOffer Ski(string name) =>
            TravelOffer.Create(
                SupplierId.New(),
                name,
                new Destination("ZRH", "Zermatt"),
                Money.Of(180.00m, Currency.Usd),
                Money.Of(22.00m, Currency.Usd),
                [OfferTag.Ski],
                4,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 6, 30));
    }
}
