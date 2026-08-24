using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Pricing;

/// <summary>
/// T-016: the named pricing properties that the earlier tasks only proved in isolation.
/// </summary>
public sealed class PricingSuiteTests
{
    [Fact]
    public void More_specific_markup_wins_over_a_higher_priority_partner_wide_rule()
    {
        var partner = PartnerId.New();
        var offer = PricingExamples.OceanicBeachOffer();
        var asOf = PricingExamples.AsOf;
        PricingRule[] rules =
        [
            BaseMarkupRule.Create(partner, Percent.From(50m), RuleScope.PartnerWide, asOf, priority: 100),
            BaseMarkupRule.Create(partner, Percent.From(12m), new RuleScope(tier: TierCode.Gold), asOf, priority: 0),
        ];

        var gold = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner, offer, TierCode.Gold, rules));
        var standard = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner, offer, TierCode.Standard, rules));

        gold.RunningTotal.Amount.Should().Be(128.80m, "Gold must take the scoped +12%, not the louder +50%");
        standard.RunningTotal.Amount.Should().Be(172.50m, "Standard only matches the partner-wide markup");
    }

    [Fact]
    public void Floor_clamps_a_stacked_discount_that_would_sell_below_the_minimum()
    {
        var partner = PartnerId.New();
        var offer = PricingExamples.OceanicBeachOffer();
        var withFloor = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner, offer, TierCode.Gold, PricingExamples.SummitRules(partner)));
        var unclamped = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner, offer, TierCode.Gold, SummitWithoutFloor(partner)));

        After(withFloor, PricingStageKind.CampaignDiscount).Amount.Should().Be(118.6892m);
        After(withFloor, PricingStageKind.MarginFloor).Amount.Should().Be(120.75m);
        withFloor.Trace.Should().Contain(e => e.Stage == PricingStageKind.MarginFloor && e.WasClamped);
        withFloor.RunningTotal.Amount.Should().Be(120.75m);

        unclamped.Trace.Should().NotContain(e => e.WasClamped);
        unclamped.RunningTotal.Amount.Should().Be(118.69m);
        unclamped.RunningTotal.Should().BeLessThan(withFloor.RunningTotal);
    }

    [Fact]
    public void Rounding_runs_once_after_full_precision_intermediates()
    {
        var partner = PartnerId.New();
        var offer = PricingExamples.OceanicBeachOffer();
        var asOf = PricingExamples.AsOf;
        PricingRule[] rules =
        [
            BaseMarkupRule.Create(partner, Percent.From(12m), RuleScope.PartnerWide, asOf),
            TierAdjustmentRule.Create(partner, Percent.From(-3m), new RuleScope(tier: TierCode.Gold), asOf),
        ];

        var state = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner, offer, TierCode.Gold, rules));

        After(state, PricingStageKind.BaseMarkup).Amount.Should().Be(128.80m);
        After(state, PricingStageKind.TierAdjustment).Amount.Should().Be(124.936m);
        IsCents(After(state, PricingStageKind.TierAdjustment)).Should().BeFalse();
        After(state, PricingStageKind.Rounding).Amount.Should().Be(124.94m);
        IsCents(After(state, PricingStageKind.Rounding)).Should().BeTrue();
        state.RunningTotal.Amount.Should().Be(124.94m);

        var roundedStages = state.Trace
            .Where(e => e.SubtotalAfter != e.SubtotalBefore && IsCents(e.SubtotalAfter) && !IsCents(e.SubtotalBefore))
            .Select(e => e.Stage)
            .ToList();

        roundedStages.Should().Equal(PricingStageKind.Rounding);
    }

    [Fact]
    public void Expired_campaign_does_not_change_the_price()
    {
        var partner = PartnerId.New();
        var offer = PricingExamples.OceanicBeachOffer();
        var january = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var march = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var april = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        PricingRule[] rules =
        [
            BaseMarkupRule.Create(partner, Percent.From(12m), RuleScope.PartnerWide, january),
            TierAdjustmentRule.Create(partner, Percent.From(-3m), new RuleScope(tier: TierCode.Gold), january),
            CampaignDiscountRule.Create(
                partner,
                "MARCH-BEACH",
                Percent.From(-5m),
                new RuleScope(tag: OfferTag.Beach),
                march,
                april),
            MarginFloorRule.Create(partner, Percent.From(5m), RuleScope.PartnerWide, january),
        ];

        var inCampaign = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner, offer, TierCode.Gold, rules, march.AddDays(14)));
        var afterCampaign = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner, offer, TierCode.Gold, rules, april));

        inCampaign.RunningTotal.Amount.Should().Be(120.75m);
        inCampaign.Trace.Should().Contain(e => e.WasClamped);
        afterCampaign.RunningTotal.Amount.Should().Be(124.94m);
        afterCampaign.Trace.Should().NotContain(e => e.WasClamped);
        afterCampaign.Trace.Should().Contain(e =>
            e.Stage == PricingStageKind.CampaignDiscount && e.Description.Contains("No campaign", StringComparison.Ordinal));
    }

    [Fact]
    public void The_same_offer_prices_differently_for_two_partners()
    {
        var offer = PricingExamples.OceanicBeachOffer();
        var summit = PartnerId.New();
        var nimbus = PartnerId.New();

        var summitPrice = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(summit, offer, TierCode.Gold, PricingExamples.SummitRules(summit)));
        var nimbusPrice = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(nimbus, offer, tier: null, PricingExamples.NimbusRules(nimbus)));

        summitPrice.RunningTotal.Amount.Should().Be(120.75m);
        nimbusPrice.RunningTotal.Amount.Should().Be(135.70m);
        summitPrice.MaxCreditTender!.Value.Amount.Should().Be(48.30m);
        nimbusPrice.MaxCreditTender!.Value.Amount.Should().Be(135.70m);

        var memberView = PriceExplanation.From(summitPrice, AccessRole.Member);
        memberView.RevealsNetRate.Should().BeFalse();
        memberView.NetCost.Should().BeNull();
        memberView.Stages.Should().Contain(e => e.WasClamped);
    }

    private static List<PricingRule> SummitWithoutFloor(PartnerId partner) =>
        [.. PricingExamples.SummitRules(partner).Where(rule => rule.Kind != PricingRuleKind.MarginFloor)];

    private static Money After(PricingState state, PricingStageKind stage) =>
        state.Trace.Single(e => e.Stage == stage).SubtotalAfter;

    private static bool IsCents(Money money) =>
        money.Amount == decimal.Round(money.Amount, 2, MidpointRounding.AwayFromZero);
}
