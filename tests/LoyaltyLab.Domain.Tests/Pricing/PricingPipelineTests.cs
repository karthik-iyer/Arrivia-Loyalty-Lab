using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Pricing;

public sealed class PricingPipelineTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Stay = new(2026, 3, 15);
    private static readonly PricingPipeline Pipeline = new();

    [Fact]
    public void Stages_run_in_documented_order()
    {
        Pipeline.Stages.Select(s => s.Order).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        Pipeline.Stages.Select(s => s.Kind).Should().Equal(
            PricingStageKind.Eligibility,
            PricingStageKind.BaseCost,
            PricingStageKind.BaseMarkup,
            PricingStageKind.TierAdjustment,
            PricingStageKind.CampaignDiscount,
            PricingStageKind.MarginFloor,
            PricingStageKind.Rounding,
            PricingStageKind.BurnCap);
    }

    [Fact]
    public void Summit_gold_in_march_matches_the_worked_example()
    {
        var partner = PartnerId.New();
        var offer = OceanicBeachOffer();
        var state = Pipeline.Execute(Request(partner, offer, TierCode.Gold, SummitRules(partner)));

        state.IsRejected.Should().BeFalse();
        state.NetCost.Amount.Should().Be(115.00m);
        state.RunningTotal.Amount.Should().Be(120.75m);
        state.MaxCreditTender!.Value.Amount.Should().Be(48.30m);
    }

    [Fact]
    public void Nimbus_without_tiers_matches_the_worked_example()
    {
        var partner = PartnerId.New();
        var offer = OceanicBeachOffer();
        var state = Pipeline.Execute(Request(partner, offer, tier: null, NimbusRules(partner)));

        state.IsRejected.Should().BeFalse();
        state.NetCost.Amount.Should().Be(115.00m);
        state.RunningTotal.Amount.Should().Be(135.70m);
        state.MaxCreditTender!.Value.Amount.Should().Be(135.70m);
    }

    [Fact]
    public void Ineligible_supplier_short_circuits_before_markup()
    {
        var partner = PartnerId.New();
        var offer = OceanicBeachOffer();
        var request = new PricingRequest(
            PricingContext.ForOffer(partner, offer, TierCode.Gold, Stay),
            offer,
            PermittedSuppliers: new HashSet<SupplierId>(),
            SummitRules(partner),
            AsOf);

        var state = Pipeline.Execute(request);

        state.IsRejected.Should().BeTrue();
        state.RejectionReason.Should().Be(Errors.OfferNotEligible);
        state.RunningTotal.Amount.Should().Be(0m);
    }

    [Fact]
    public void Exclusion_rule_rejects_the_offer()
    {
        var partner = PartnerId.New();
        var offer = OceanicBeachOffer();
        var exclusion = EligibilityExclusionRule.Create(
            partner,
            new RuleScope(supplierId: offer.SupplierId),
            AsOf);
        var rules = SummitRules(partner).Append(exclusion).ToList();

        var state = Pipeline.Execute(Request(partner, offer, TierCode.Gold, rules));

        state.IsRejected.Should().BeTrue();
        state.RejectionReason.Should().Be(Errors.OfferNotEligible);
    }

    private static PricingRequest Request(
        PartnerId partner,
        TravelOffer offer,
        TierCode? tier,
        IReadOnlyList<PricingRule> rules) =>
        new(
            PricingContext.ForOffer(partner, offer, tier, Stay),
            offer,
            new HashSet<SupplierId> { offer.SupplierId },
            rules,
            AsOf);

    private static TravelOffer OceanicBeachOffer() =>
        TravelOffer.Create(
            SupplierId.New(),
            "Coral Bay Resort",
            new Destination("MBJ", "Montego Bay"),
            Money.Of(100.00m, Currency.Usd),
            Money.Of(15.00m, Currency.Usd),
            [OfferTag.Beach],
            starRating: 4,
            availableFrom: new DateOnly(2026, 1, 1),
            availableTo: new DateOnly(2026, 6, 30));

    private static List<PricingRule> SummitRules(PartnerId partner) =>
    [
        BaseMarkupRule.Create(partner, Percent.From(12m), RuleScope.PartnerWide, AsOf),
        TierAdjustmentRule.Create(partner, Percent.From(-3m), new RuleScope(tier: TierCode.Gold), AsOf),
        CampaignDiscountRule.Create(
            partner,
            "MARCH-BEACH",
            Percent.From(-5m),
            new RuleScope(tag: OfferTag.Beach),
            AsOf),
        MarginFloorRule.Create(partner, Percent.From(5m), RuleScope.PartnerWide, AsOf),
        BurnCapRule.Create(partner, Percent.From(40m), RuleScope.PartnerWide, AsOf),
    ];

    private static List<PricingRule> NimbusRules(PartnerId partner) =>
    [
        BaseMarkupRule.Create(partner, Percent.From(18m), RuleScope.PartnerWide, AsOf),
        MarginFloorRule.Create(partner, Percent.From(5m), RuleScope.PartnerWide, AsOf),
        BurnCapRule.Create(partner, Percent.From(100m), RuleScope.PartnerWide, AsOf),
    ];
}
