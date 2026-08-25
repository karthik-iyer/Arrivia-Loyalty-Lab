using System.Globalization;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Concierge;

/// <summary>
/// Partner-eligible inventory → affordability → weighted rank. Pure; quoting is the caller's job (FR-C-04).
/// </summary>
public static class CandidatePipeline
{
    public static RecommendationSet Evaluate(RecommendationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var weights = request.Weights ?? RankingWeights.Default;
        var exclusions = new List<ExclusionRecord>();
        var kept = new List<PricedCandidate>();
        var criteria = request.Criteria;

        foreach (var offer in request.Catalog)
        {
            if (!request.PermittedSuppliers.Contains(offer.SupplierId))
            {
                exclusions.Add(new ExclusionRecord(
                    offer.Id,
                    ExclusionReason.SupplierNotPermitted,
                    "Supplier is not permitted for this partner."));
                continue;
            }

            if (criteria.StayDate is { } stay
                && (stay < offer.AvailableFrom || stay > offer.AvailableTo))
            {
                exclusions.Add(new ExclusionRecord(
                    offer.Id,
                    ExclusionReason.OutsideAvailability,
                    $"Not available on {stay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}."));
                continue;
            }

            if (criteria.HasDestination
                && !string.Equals(offer.Destination.Code, criteria.DestinationCode, StringComparison.OrdinalIgnoreCase))
            {
                exclusions.Add(new ExclusionRecord(
                    offer.Id,
                    ExclusionReason.DestinationMismatch,
                    $"Destination {offer.Destination.Code} does not match {criteria.DestinationCode}."));
                continue;
            }

            if (!request.Quotes.TryGetValue(offer.Id, out var quoted))
            {
                exclusions.Add(new ExclusionRecord(
                    offer.Id,
                    ExclusionReason.TierNotEntitled,
                    "Offer is excluded by tier rules."));
                continue;
            }

            if (criteria.MaxBudget is { } budget && quoted.MemberPrice > budget)
            {
                exclusions.Add(new ExclusionRecord(
                    offer.Id,
                    ExclusionReason.BudgetExceeded,
                    $"Member price {quoted.MemberPrice.Amount.ToString(CultureInfo.InvariantCulture)} exceeds budget {budget.Amount.ToString(CultureInfo.InvariantCulture)}."));
                continue;
            }

            if (!AffordabilityFilter.CanAfford(quoted.MaxCredits, request.CreditBalance))
            {
                exclusions.Add(new ExclusionRecord(
                    offer.Id,
                    ExclusionReason.UnaffordableWithCredits,
                    $"Requires {quoted.MaxCredits.ToString(CultureInfo.InvariantCulture)} credits, available {request.CreditBalance.ToString(CultureInfo.InvariantCulture)}."));
                continue;
            }

            kept.Add(quoted);
        }

        var ranked = RecommendationRanker.Rank(kept, criteria, weights);
        return new RecommendationSet(ranked, exclusions, request.InterpretedTerms, weights);
    }
}
