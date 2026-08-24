using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Pricing;

public enum PricingRuleKind
{
    EligibilityExclusion = 0,
    BaseMarkup = 1,
    TierAdjustment = 2,
    CampaignDiscount = 3,
    MarginFloor = 4,
    BurnCap = 5,
}

/// <summary>
/// Effective-dated partner rule. Subclasses are the six kinds in FR-P-02.
/// Changing a rule means closing this row and inserting another (ADR-0012).
/// </summary>
public abstract class PricingRule : Entity<PricingRuleId>, ITenantOwned
{
    protected PricingRule()
    {
        Scope = RuleScope.PartnerWide;
    }

    protected PricingRule(
        PricingRuleId id,
        PartnerId partnerId,
        PricingRuleKind kind,
        RuleScope scope,
        int priority,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(scope);
        EnsureWindow(effectiveFrom, effectiveTo);

        PartnerId = partnerId;
        Kind = kind;
        Scope = scope;
        Priority = priority;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    public PartnerId PartnerId { get; private set; }

    public PricingRuleKind Kind { get; private set; }

    public RuleScope Scope { get; private set; }

    public int Priority { get; private set; }

    public DateTimeOffset EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public int Specificity => Scope.Specificity;

    public bool AppliesTo(PricingContext context, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.PartnerId != PartnerId)
        {
            return false;
        }

        if (asOf < EffectiveFrom)
        {
            return false;
        }

        if (EffectiveTo is { } until && asOf >= until)
        {
            return false;
        }

        return Scope.Matches(context);
    }

    protected static void EnsureWindow(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
    {
        if (effectiveTo is { } until && until <= effectiveFrom)
        {
            throw new DomainException("A rule's exclusive end must be after its inclusive start.");
        }
    }
}

public sealed class EligibilityExclusionRule : PricingRule
{
    private EligibilityExclusionRule()
    {
    }

    private EligibilityExclusionRule(
        PricingRuleId id,
        PartnerId partnerId,
        RuleScope scope,
        int priority,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo)
        : base(id, partnerId, PricingRuleKind.EligibilityExclusion, scope, priority, effectiveFrom, effectiveTo)
    {
    }

    public static EligibilityExclusionRule Create(
        PartnerId partnerId,
        RuleScope scope,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo = null,
        int priority = 0,
        PricingRuleId? id = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Specificity == 0)
        {
            throw new DomainException("An eligibility exclusion must name at least one scope dimension.");
        }

        return new EligibilityExclusionRule(
            id ?? PricingRuleId.New(), partnerId, scope, priority, effectiveFrom, effectiveTo);
    }
}

public sealed class BaseMarkupRule : PricingRule
{
    private BaseMarkupRule()
    {
    }

    private BaseMarkupRule(
        PricingRuleId id,
        PartnerId partnerId,
        Percent markup,
        RuleScope scope,
        int priority,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo)
        : base(id, partnerId, PricingRuleKind.BaseMarkup, scope, priority, effectiveFrom, effectiveTo)
    {
        Markup = markup;
    }

    public Percent Markup { get; private set; }

    public static BaseMarkupRule Create(
        PartnerId partnerId,
        Percent markup,
        RuleScope scope,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo = null,
        int priority = 0,
        PricingRuleId? id = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new BaseMarkupRule(
            id ?? PricingRuleId.New(), partnerId, markup, scope, priority, effectiveFrom, effectiveTo);
    }
}

public sealed class TierAdjustmentRule : PricingRule
{
    private TierAdjustmentRule()
    {
    }

    private TierAdjustmentRule(
        PricingRuleId id,
        PartnerId partnerId,
        Percent adjustment,
        RuleScope scope,
        int priority,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo)
        : base(id, partnerId, PricingRuleKind.TierAdjustment, scope, priority, effectiveFrom, effectiveTo)
    {
        Adjustment = adjustment;
    }

    public Percent Adjustment { get; private set; }

    public static TierAdjustmentRule Create(
        PartnerId partnerId,
        Percent adjustment,
        RuleScope scope,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo = null,
        int priority = 0,
        PricingRuleId? id = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Tier is null)
        {
            throw new DomainException("A tier adjustment must name a tier.");
        }

        return new TierAdjustmentRule(
            id ?? PricingRuleId.New(), partnerId, adjustment, scope, priority, effectiveFrom, effectiveTo);
    }
}

public sealed class CampaignDiscountRule : PricingRule
{
    private CampaignDiscountRule()
    {
        CampaignCode = null!;
    }

    private CampaignDiscountRule(
        PricingRuleId id,
        PartnerId partnerId,
        string campaignCode,
        Percent adjustment,
        RuleScope scope,
        int priority,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo)
        : base(id, partnerId, PricingRuleKind.CampaignDiscount, scope, priority, effectiveFrom, effectiveTo)
    {
        CampaignCode = campaignCode;
        Adjustment = adjustment;
    }

    public string CampaignCode { get; private set; }

    public Percent Adjustment { get; private set; }

    public static CampaignDiscountRule Create(
        PartnerId partnerId,
        string campaignCode,
        Percent adjustment,
        RuleScope scope,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo = null,
        int priority = 0,
        PricingRuleId? id = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(campaignCode))
        {
            throw new DomainException("Campaign code is required.");
        }

        return new CampaignDiscountRule(
            id ?? PricingRuleId.New(),
            partnerId,
            campaignCode.Trim().ToUpperInvariant(),
            adjustment,
            scope,
            priority,
            effectiveFrom,
            effectiveTo);
    }
}

public sealed class MarginFloorRule : PricingRule
{
    private MarginFloorRule()
    {
    }

    private MarginFloorRule(
        PricingRuleId id,
        PartnerId partnerId,
        Percent floorAboveNet,
        RuleScope scope,
        int priority,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo)
        : base(id, partnerId, PricingRuleKind.MarginFloor, scope, priority, effectiveFrom, effectiveTo)
    {
        FloorAboveNet = floorAboveNet;
    }

    public Percent FloorAboveNet { get; private set; }

    public static MarginFloorRule Create(
        PartnerId partnerId,
        Percent floorAboveNet,
        RuleScope scope,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo = null,
        int priority = 0,
        PricingRuleId? id = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (floorAboveNet.Value < 0m)
        {
            throw new DomainException("A margin floor cannot be negative.");
        }

        return new MarginFloorRule(
            id ?? PricingRuleId.New(), partnerId, floorAboveNet, scope, priority, effectiveFrom, effectiveTo);
    }
}

public sealed class BurnCapRule : PricingRule
{
    private BurnCapRule()
    {
    }

    private BurnCapRule(
        PricingRuleId id,
        PartnerId partnerId,
        Percent cap,
        RuleScope scope,
        int priority,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo)
        : base(id, partnerId, PricingRuleKind.BurnCap, scope, priority, effectiveFrom, effectiveTo)
    {
        Cap = cap;
    }

    public Percent Cap { get; private set; }

    public static BurnCapRule Create(
        PartnerId partnerId,
        Percent cap,
        RuleScope scope,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo = null,
        int priority = 0,
        PricingRuleId? id = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (cap.Value is < 0m or > 100m)
        {
            throw new DomainException("A burn cap must be between 0 and 100 percent.");
        }

        return new BurnCapRule(
            id ?? PricingRuleId.New(), partnerId, cap, scope, priority, effectiveFrom, effectiveTo);
    }
}
