using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Concierge;

namespace LoyaltyLab.Application.Concierge;

public sealed record RecommendCommand(
    string? Text,
    DateOnly? StayDate = null,
    string? DestinationCode = null,
    decimal? MaxBudget = null);

public sealed record RecommendedOffer(
    OfferId OfferId,
    string PropertyName,
    QuoteId QuoteId,
    Money MemberPrice,
    int CreditsCover,
    decimal Score,
    IReadOnlyList<string> Reasons);

public sealed record RecommendResult(
    string Narrative,
    bool NarrationApplied,
    IReadOnlyList<RecommendedOffer> Recommendations,
    RecommendationAudit Audit);
