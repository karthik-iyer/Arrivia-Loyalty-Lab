using System.Globalization;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Concierge;

/// <summary>
/// Deterministic ranking over priced, already-eligible candidates (docs/04 §5.1).
/// </summary>
public static class RecommendationRanker
{
    public static IReadOnlyList<RankedRecommendation> Rank(
        IReadOnlyList<PricedCandidate> candidates,
        RecommendationCriteria criteria,
        RankingWeights weights)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(weights);

        if (candidates.Count == 0)
        {
            return [];
        }

        var minPrice = candidates.Min(candidate => candidate.MemberPrice.Amount);
        var maxPrice = candidates.Max(candidate => candidate.MemberPrice.Amount);
        var span = maxPrice - minPrice;

        return candidates
            .Select(candidate => Score(candidate, criteria, weights, minPrice, span))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.PropertyName, StringComparer.Ordinal)
            .ThenBy(item => item.OfferId.Value)
            .ToArray();
    }

    private static RankedRecommendation Score(
        PricedCandidate candidate,
        RecommendationCriteria criteria,
        RankingWeights weights,
        decimal minPrice,
        decimal priceSpan)
    {
        var valueForMoney = priceSpan == 0m
            ? 1m
            : 1m - ((candidate.MemberPrice.Amount - minPrice) / priceSpan);

        var creditCoverage = candidate.MemberPrice.IsZero
            ? 1m
            : Clamp01(candidate.MaxCreditTender.Amount / candidate.MemberPrice.Amount);

        var tagMatch = TagMatch(candidate.Offer, criteria);
        var starRating = candidate.Offer.StarRating / 5m;
        var score = (weights.ValueForMoney * valueForMoney)
            + (weights.CreditCoverage * creditCoverage)
            + (weights.TagMatch * tagMatch)
            + (weights.StarRating * starRating);

        return new RankedRecommendation(
            candidate.Offer.Id,
            candidate.Offer.PropertyName,
            candidate.MemberPrice,
            candidate.MaxCredits,
            score,
            Reasons(valueForMoney, creditCoverage, criteria, candidate.Offer));
    }

    private static decimal TagMatch(TravelOffer offer, RecommendationCriteria criteria)
    {
        if (!criteria.HasTags)
        {
            return 1m;
        }

        var hits = criteria.Tags.Count(tag => offer.Tags.Contains(tag));
        return hits / (decimal)criteria.Tags.Count;
    }

    private static List<string> Reasons(
        decimal valueForMoney,
        decimal creditCoverage,
        RecommendationCriteria criteria,
        TravelOffer offer)
    {
        var reasons = new List<string>();
        if (valueForMoney >= 0.75m)
        {
            reasons.Add("Strong value for money");
        }

        var coverPercent = (int)decimal.Round(creditCoverage * 100m, 0, MidpointRounding.AwayFromZero);
        if (coverPercent > 0)
        {
            reasons.Add($"Credits cover {coverPercent.ToString(CultureInfo.InvariantCulture)}%");
        }

        if (criteria.HasTags)
        {
            var matched = criteria.Tags
                .Where(offer.Tags.Contains)
                .Select(tag => tag.ToString().ToLowerInvariant())
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (matched.Length > 0)
            {
                reasons.Add("Matches: " + string.Join(", ", matched));
            }
        }

        return reasons;
    }

    private static decimal Clamp01(decimal value) =>
        value < 0m ? 0m : value > 1m ? 1m : value;
}
