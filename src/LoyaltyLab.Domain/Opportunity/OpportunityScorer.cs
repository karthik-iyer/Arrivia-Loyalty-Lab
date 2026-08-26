using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Opportunity;

/// <summary>
/// Deterministic signal scoring (docs/04 §6.2, FR-O-04). Weights sum to 1 so the total is a convex combination.
/// </summary>
public static class OpportunityScorer
{
    public const int TypicalStayNights = 7;

    public const int DestinationAffinitySaturation = 3;

    public const decimal PriceDropSaturation = 0.30m;

    public static IReadOnlyList<OpportunitySignal> Score(
        TravelWindow window,
        TravelOffer offer,
        OpportunityPolicy policy,
        IReadOnlyList<CompletedStay> history,
        Money memberPrice,
        Money maxCreditTender,
        Money creditBalance,
        Money? watchedBaselineNetRate)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(history);

        var weights = policy.Weights;
        var historicalTags = history
            .SelectMany(stay => stay.Tags)
            .ToHashSet();

        return
        [
            WindowFit(window, weights.WindowFit),
            DestinationAffinity(offer.Destination, history, weights.DestinationAffinity),
            TagAffinity(offer.Tags, historicalTags, weights.TagAffinity),
            CreditCoverage(memberPrice, maxCreditTender, creditBalance, weights.CreditCoverage),
            PriceDrop(offer.NetRate, watchedBaselineNetRate, policy.PriceDropThreshold, weights.PriceDrop),
        ];
    }

    public static decimal Total(IReadOnlyList<OpportunitySignal> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);
        return signals.Sum(signal => signal.Contribution);
    }

    private static OpportunitySignal WindowFit(TravelWindow window, decimal weight)
    {
        var normalized = Clamp01(window.Nights / (decimal)TypicalStayNights);
        return OpportunitySignal.Of(SignalKind.WindowFit, window.Nights, normalized, weight);
    }

    private static OpportunitySignal DestinationAffinity(
        Destination destination,
        IReadOnlyList<CompletedStay> history,
        decimal weight)
    {
        var visits = history.Count(stay => stay.Destination.Code == destination.Code);
        var normalized = Clamp01(visits / (decimal)DestinationAffinitySaturation);
        return OpportunitySignal.Of(SignalKind.DestinationAffinity, visits, normalized, weight);
    }

    private static OpportunitySignal TagAffinity(
        IReadOnlySet<OfferTag> offerTags,
        IReadOnlySet<OfferTag> historicalTags,
        decimal weight)
    {
        var jaccard = Jaccard(offerTags, historicalTags);
        return OpportunitySignal.Of(SignalKind.TagAffinity, jaccard, jaccard, weight);
    }

    private static OpportunitySignal CreditCoverage(
        Money memberPrice,
        Money maxCreditTender,
        Money creditBalance,
        decimal weight)
    {
        var payable = Money.Of(
            Math.Min(creditBalance.Amount, maxCreditTender.Amount),
            memberPrice.Currency);
        var share = memberPrice.IsZero ? 1m : Clamp01(payable.Amount / memberPrice.Amount);
        return OpportunitySignal.Of(SignalKind.CreditCoverage, share, share, weight);
    }

    private static OpportunitySignal PriceDrop(
        Money currentNet,
        Money? baselineNet,
        Percent threshold,
        decimal weight)
    {
        if (baselineNet is not { } baseline || baseline.Amount <= 0m || currentNet.Currency != baseline.Currency)
        {
            return OpportunitySignal.Of(SignalKind.PriceDrop, 0m, 0m, weight);
        }

        var drop = Clamp01((baseline.Amount - currentNet.Amount) / baseline.Amount);
        var normalized = drop < threshold.AsFraction() ? 0m : Clamp01(drop / PriceDropSaturation);
        return OpportunitySignal.Of(SignalKind.PriceDrop, drop, normalized, weight);
    }

    private static decimal Jaccard(IReadOnlySet<OfferTag> left, IReadOnlySet<OfferTag> right)
    {
        if (left.Count == 0 && right.Count == 0)
        {
            return 0m;
        }

        var intersection = left.Count(right.Contains);
        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0m : intersection / (decimal)union;
    }

    private static decimal Clamp01(decimal value) =>
        value < 0m ? 0m : value > 1m ? 1m : value;
}
