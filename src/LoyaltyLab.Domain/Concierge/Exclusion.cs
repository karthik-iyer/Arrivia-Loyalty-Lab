using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Concierge;

public enum ExclusionReason
{
    SupplierNotPermitted = 0,
    TierNotEntitled = 1,
    OutsideAvailability = 2,
    UnaffordableWithCredits = 3,
    BudgetExceeded = 4,
    DestinationMismatch = 5,
}

public sealed record ExclusionRecord(OfferId OfferId, ExclusionReason Reason, string Detail);
