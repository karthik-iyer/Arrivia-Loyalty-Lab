using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Concierge;

namespace LoyaltyLab.Application.Abstractions;

/// <summary>
/// Optional prose over an already-ranked result. Implementations must not query inventory or prices.
/// </summary>
public interface IOfferNarrator
{
    Task<Result<string>> NarrateAsync(RecommendationSet facts, CancellationToken cancellationToken);
}
