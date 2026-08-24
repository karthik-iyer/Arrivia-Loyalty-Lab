using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Pricing;

public sealed class RuleScopeTests
{
    [Fact]
    public void Partner_wide_scope_has_zero_specificity()
    {
        RuleScope.PartnerWide.Specificity.Should().Be(0);
    }

    [Fact]
    public void Specificity_counts_each_populated_dimension()
    {
        var scope = new RuleScope(
            tier: TierCode.Gold,
            tag: OfferTag.Beach,
            destinationCode: "mbj");

        scope.Specificity.Should().Be(3);
        scope.DestinationCode.Should().Be("MBJ");
    }

    [Fact]
    public void A_tier_scoped_rule_does_not_match_a_different_tier()
    {
        var gold = new RuleScope(tier: TierCode.Gold);
        var context = Context(tier: TierCode.Standard);

        gold.Matches(context).Should().BeFalse();
    }

    [Fact]
    public void A_tag_scoped_rule_matches_when_the_offer_has_that_tag()
    {
        var beach = new RuleScope(tag: OfferTag.Beach);
        beach.Matches(Context()).Should().BeTrue();
        new RuleScope(tag: OfferTag.Ski).Matches(Context()).Should().BeFalse();
    }

    private static PricingContext Context(TierCode? tier = TierCode.Gold) =>
        new(
            PartnerId.New(),
            SupplierId.New(),
            OfferId.New(),
            "MBJ",
            [OfferTag.Beach, OfferTag.Family],
            tier,
            new DateOnly(2026, 3, 15));
}

public sealed class PricingRuleAppliesToTests
{
    private static readonly DateTimeOffset March = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Inclusive_start_and_exclusive_end()
    {
        var partner = PartnerId.New();
        var rule = Markup(
            partner,
            effectiveFrom: March,
            effectiveTo: March.AddDays(1));

        var context = Context(partner);
        rule.AppliesTo(context, March).Should().BeTrue();
        rule.AppliesTo(context, March.AddDays(1)).Should().BeFalse();
        rule.AppliesTo(context, March.AddTicks(-1)).Should().BeFalse();
    }

    [Fact]
    public void Open_ended_rule_applies_after_its_start()
    {
        var partner = PartnerId.New();
        var rule = Markup(partner, effectiveFrom: March, effectiveTo: null);

        rule.AppliesTo(Context(partner), March.AddYears(1)).Should().BeTrue();
    }

    [Fact]
    public void A_rule_does_not_apply_to_another_partner()
    {
        var rule = Markup(PartnerId.New(), March);
        rule.AppliesTo(Context(PartnerId.New()), March).Should().BeFalse();
    }

    [Fact]
    public void Empty_effective_window_is_rejected()
    {
        var act = () => Markup(PartnerId.New(), March, effectiveTo: March);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Exclusion_requires_a_scope_dimension()
    {
        var act = () => EligibilityExclusionRule.Create(
            PartnerId.New(),
            RuleScope.PartnerWide,
            March);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Tier_adjustment_requires_a_tier_on_the_scope()
    {
        var act = () => TierAdjustmentRule.Create(
            PartnerId.New(),
            Percent.From(-3m),
            RuleScope.PartnerWide,
            March);

        act.Should().Throw<DomainException>();
    }

    private static BaseMarkupRule Markup(
        PartnerId partnerId,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo = null,
        RuleScope? scope = null,
        int priority = 0,
        PricingRuleId? id = null) =>
        BaseMarkupRule.Create(
            partnerId,
            Percent.From(12m),
            scope ?? RuleScope.PartnerWide,
            effectiveFrom,
            effectiveTo,
            priority,
            id);

    private static PricingContext Context(PartnerId partnerId) =>
        new(partnerId, SupplierId.New(), OfferId.New(), "MBJ", [OfferTag.Beach], TierCode.Gold, new DateOnly(2026, 3, 15));
}

public sealed class PricingRulePrecedenceTests
{
    private static readonly DateTimeOffset January = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset March = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly PartnerId Partner = PartnerId.New();

    [Fact]
    public void Higher_specificity_beats_higher_priority()
    {
        var wide = Markup(RuleScope.PartnerWide, priority: 100, from: January, id: Id(2));
        var goldBeach = Markup(
            new RuleScope(tier: TierCode.Gold, tag: OfferTag.Beach),
            priority: 0,
            from: January,
            id: Id(1));

        Winner(wide, goldBeach).Should().Be(goldBeach);
    }

    [Fact]
    public void Equal_specificity_defers_to_priority()
    {
        var low = Markup(RuleScope.PartnerWide, priority: 1, from: January, id: Id(1));
        var high = Markup(RuleScope.PartnerWide, priority: 9, from: January, id: Id(2));

        Winner(low, high).Should().Be(high);
    }

    [Fact]
    public void Equal_specificity_and_priority_prefers_the_later_activation()
    {
        var older = Markup(RuleScope.PartnerWide, priority: 1, from: January, id: Id(2));
        var newer = Markup(RuleScope.PartnerWide, priority: 1, from: March, id: Id(1));

        Winner(older, newer).Should().Be(newer);
    }

    [Fact]
    public void Remaining_ties_break_on_rule_id_ascending()
    {
        var higherId = Markup(RuleScope.PartnerWide, priority: 1, from: January, id: Id(2));
        var lowerId = Markup(RuleScope.PartnerWide, priority: 1, from: January, id: Id(1));

        Winner(higherId, lowerId).Should().Be(lowerId);
    }

    [Fact]
    public void Ordering_is_total_no_distinct_pair_ties()
    {
        var rules = CartesianRules();
        rules.Select(r => r.Id).Should().OnlyHaveUniqueItems();

        for (var i = 0; i < rules.Count; i++)
        {
            for (var j = 0; j < rules.Count; j++)
            {
                var cmp = PricingRulePrecedenceComparer.Instance.Compare(rules[i], rules[j]);
                var reverse = PricingRulePrecedenceComparer.Instance.Compare(rules[j], rules[i]);

                if (i == j)
                {
                    cmp.Should().Be(0);
                    continue;
                }

                cmp.Should().NotBe(0, "distinct rules {0} and {1} must not tie", rules[i].Id, rules[j].Id);
                Math.Sign(cmp).Should().Be(-Math.Sign(reverse));
            }
        }

        var sorted = rules.OrderBy(r => r, PricingRulePrecedenceComparer.Instance).ToList();
        for (var i = 0; i < sorted.Count - 1; i++)
        {
            PricingRulePrecedenceComparer.Instance.Compare(sorted[i], sorted[i + 1])
                .Should().BeNegative();
        }
    }

    private static List<PricingRule> CartesianRules()
    {
        RuleScope[] scopes =
        [
            RuleScope.PartnerWide,
            new RuleScope(tier: TierCode.Gold),
            new RuleScope(tier: TierCode.Gold, tag: OfferTag.Beach),
        ];
        int[] priorities = [0, 5];
        DateTimeOffset[] starts = [January, March];

        var id = 1;
        var rules = new List<PricingRule>();
        foreach (var scope in scopes)
        {
            foreach (var priority in priorities)
            {
                foreach (var start in starts)
                {
                    rules.Add(Markup(scope, priority, start, Id(id++)));
                    rules.Add(Markup(scope, priority, start, Id(id++)));
                }
            }
        }

        return rules;
    }

    private static BaseMarkupRule Markup(
        RuleScope scope,
        int priority,
        DateTimeOffset from,
        PricingRuleId id) =>
        BaseMarkupRule.Create(Partner, Percent.From(12m), scope, from, effectiveTo: null, priority, id);

    private static PricingRule Winner(params PricingRule[] rules) =>
        PricingRulePrecedenceComparer.SelectWinner(rules)!;

    private static PricingRuleId Id(int n) =>
        new(Guid.Parse($"a11ce010-0010-7000-8000-{n:D12}"));
}
