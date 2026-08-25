namespace LoyaltyLab.Domain.Concierge;

/// <summary>
/// A recommendation is affordable with credits when the burn-capped tender fits the live balance (FR-C-03).
/// </summary>
public static class AffordabilityFilter
{
    public static bool CanAfford(int maxCredits, int creditBalance) =>
        maxCredits <= creditBalance;
}
