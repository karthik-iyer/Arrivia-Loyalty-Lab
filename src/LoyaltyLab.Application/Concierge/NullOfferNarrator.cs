using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Concierge;

namespace LoyaltyLab.Application.Concierge;

/// <summary>
/// Default narrator: the template, no key, no network (FR-C-07).
/// </summary>
public sealed class NullOfferNarrator : IOfferNarrator
{
    public Task<Result<string>> NarrateAsync(RecommendationSet facts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(facts);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Result<string>.Success(NarrationTemplate.Render(facts)));
    }
}
