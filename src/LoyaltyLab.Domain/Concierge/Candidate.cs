using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Concierge;

/// <summary>
/// A catalog row after the pricing engine has produced a member price (FR-C-04).
/// Quote identity is assigned by the application layer when it persists a quote.
/// </summary>
public sealed record PricedCandidate(
    TravelOffer Offer,
    Money MemberPrice,
    Money MaxCreditTender,
    int MaxCredits);

public sealed record RankedRecommendation(
    OfferId OfferId,
    string PropertyName,
    Money MemberPrice,
    int CreditsCover,
    decimal Score,
    IReadOnlyList<string> Reasons);

public sealed record RecommendationSet(
    IReadOnlyList<RankedRecommendation> Recommendations,
    IReadOnlyList<ExclusionRecord> Exclusions,
    IReadOnlyList<string> InterpretedTerms,
    RankingWeights Weights);

public sealed record RecommendationRequest(
    RecommendationCriteria Criteria,
    IReadOnlyList<string> InterpretedTerms,
    IReadOnlyList<TravelOffer> Catalog,
    IReadOnlySet<SupplierId> PermittedSuppliers,
    IReadOnlyDictionary<OfferId, PricedCandidate> Quotes,
    int CreditBalance,
    RankingWeights? Weights = null);
