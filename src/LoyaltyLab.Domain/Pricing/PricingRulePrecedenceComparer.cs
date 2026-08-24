namespace LoyaltyLab.Domain.Pricing;

/// <summary>
/// Total order within a kind (FR-P-04). Distinct rules never compare equal.
/// Eligibility exclusions are a gate — every match applies — so callers filter by kind first.
/// </summary>
public sealed class PricingRulePrecedenceComparer : IComparer<PricingRule>
{
    public static PricingRulePrecedenceComparer Instance { get; } = new();

    public int Compare(PricingRule? x, PricingRule? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        if (x.Id == y.Id)
        {
            return 0;
        }

        var specificity = y.Specificity.CompareTo(x.Specificity);
        if (specificity != 0)
        {
            return specificity;
        }

        var priority = y.Priority.CompareTo(x.Priority);
        if (priority != 0)
        {
            return priority;
        }

        var activated = y.EffectiveFrom.CompareTo(x.EffectiveFrom);
        if (activated != 0)
        {
            return activated;
        }

        return x.Id.Value.CompareTo(y.Id.Value);
    }

    public static PricingRule? SelectWinner(IEnumerable<PricingRule> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates.OrderBy(rule => rule, Instance).FirstOrDefault();
    }
}
