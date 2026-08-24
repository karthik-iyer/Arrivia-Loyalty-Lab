using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Pricing;

public sealed class PricingPipelineTests
{
    [Fact]
    public void Stages_run_in_documented_order()
    {
        PricingExamples.Pipeline.Stages.Select(s => s.Order).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        PricingExamples.Pipeline.Stages.Select(s => s.Kind).Should().Equal(
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
        var offer = PricingExamples.OceanicBeachOffer();
        var state = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner, offer, TierCode.Gold, PricingExamples.SummitRules(partner)));

        state.IsRejected.Should().BeFalse();
        state.NetCost.Amount.Should().Be(115.00m);
        state.RunningTotal.Amount.Should().Be(120.75m);
        state.MaxCreditTender!.Value.Amount.Should().Be(48.30m);
        state.Trace.Should().Contain(e => e.WasClamped);
    }

    [Fact]
    public void Nimbus_without_tiers_matches_the_worked_example()
    {
        var partner = PartnerId.New();
        var offer = PricingExamples.OceanicBeachOffer();
        var state = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner, offer, tier: null, PricingExamples.NimbusRules(partner)));

        state.IsRejected.Should().BeFalse();
        state.NetCost.Amount.Should().Be(115.00m);
        state.RunningTotal.Amount.Should().Be(135.70m);
        state.MaxCreditTender!.Value.Amount.Should().Be(135.70m);
        state.Trace.Should().NotContain(e => e.WasClamped);
    }

    [Fact]
    public void Ineligible_supplier_short_circuits_before_markup()
    {
        var partner = PartnerId.New();
        var offer = PricingExamples.OceanicBeachOffer();
        var request = new PricingRequest(
            PricingContext.ForOffer(partner, offer, TierCode.Gold, PricingExamples.Stay),
            offer,
            PermittedSuppliers: new HashSet<SupplierId>(),
            PricingExamples.SummitRules(partner),
            PricingExamples.AsOf);

        var state = PricingExamples.Pipeline.Execute(request);

        state.IsRejected.Should().BeTrue();
        state.RejectionReason.Should().Be(Errors.OfferNotEligible);
        state.RunningTotal.Amount.Should().Be(0m);
        state.Trace.Should().ContainSingle(e => e.Stage == PricingStageKind.Eligibility);
    }

    [Fact]
    public void Exclusion_rule_rejects_the_offer()
    {
        var partner = PartnerId.New();
        var offer = PricingExamples.OceanicBeachOffer();
        var exclusion = EligibilityExclusionRule.Create(
            partner,
            new RuleScope(supplierId: offer.SupplierId),
            PricingExamples.AsOf);
        var rules = PricingExamples.SummitRules(partner).Append(exclusion).ToList();

        var state = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner, offer, TierCode.Gold, rules));

        state.IsRejected.Should().BeTrue();
        state.RejectionReason.Should().Be(Errors.OfferNotEligible);
    }
}

public sealed class PriceExplanationTests
{
    [Fact]
    public void Internal_trace_includes_net_cost_margin_and_the_clamp()
    {
        var state = PriceSummit();
        var explanation = PriceExplanation.From(state, AccessRole.AccountManager);

        explanation.NetCost!.Value.Amount.Should().Be(115.00m);
        explanation.Margin!.Value.Amount.Should().Be(5.75m);
        explanation.MemberPrice.Amount.Should().Be(120.75m);
        explanation.Stages.Should().Contain(e => e.Stage == PricingStageKind.BaseCost);
        explanation.Stages.Should().Contain(e => e.WasClamped && e.ClampReason!.Contains("net cost", StringComparison.Ordinal));
    }

    [Fact]
    public void Member_projection_contains_no_net_rate()
    {
        var state = PriceSummit();
        var explanation = PriceExplanation.From(state, AccessRole.Member);

        explanation.NetCost.Should().BeNull();
        explanation.Margin.Should().BeNull();
        explanation.RevealsNetRate.Should().BeFalse();
        explanation.Stages.Should().NotContain(e => e.Stage == PricingStageKind.BaseCost);
        explanation.Stages.Should().NotContain(e => e.Stage == PricingStageKind.Eligibility);
        explanation.Stages.Should().OnlyContain(e => e.Order >= 3);

        var leaked = Flatten(explanation);
        leaked.Should().NotContain("100");
        leaked.Should().NotContain("115");
        leaked.Should().NotContain("net cost");
        leaked.Should().NotContain("net 100");

        explanation.Stages.Should().Contain(e => e.WasClamped);
        explanation.Stages.Single(e => e.WasClamped).ClampReason.Should().Contain("partner minimum");
        explanation.Stages[0].SubtotalBefore.Amount.Should().Be(0m);
    }

    [Fact]
    public void Anonymous_projection_matches_member_visibility()
    {
        var state = PriceSummit();
        var member = PriceExplanation.From(state, AccessRole.Member);
        var anonymous = PriceExplanation.From(state, AccessRole.Anonymous);

        anonymous.NetCost.Should().BeNull();
        anonymous.Margin.Should().BeNull();
        anonymous.Stages.Select(s => s.Stage).Should().Equal(member.Stages.Select(s => s.Stage));
    }

    private static PricingState PriceSummit()
    {
        var partner = PartnerId.New();
        var offer = PricingExamples.OceanicBeachOffer();
        return PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner, offer, TierCode.Gold, PricingExamples.SummitRules(partner)));
    }

    private static string Flatten(PriceExplanation explanation) =>
        string.Join(
            '|',
            explanation.Stages.Select(e =>
                $"{e.Description}:{e.SubtotalBefore.Amount}:{e.SubtotalAfter.Amount}:{e.ClampReason}"));
}
