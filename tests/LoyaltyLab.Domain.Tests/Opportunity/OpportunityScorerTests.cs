using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;

namespace LoyaltyLab.Domain.Tests.Opportunity;

public sealed class OpportunityScorerTests
{
    [Fact]
    public void Fourteen_nights_saturate_window_fit()
    {
        var signals = Score(nights: 14, visits: 0, historyTags: [], creditShare: (120.75m, 48.30m, 60m));

        Signal(signals, SignalKind.WindowFit).RawValue.Should().Be(14m);
        Signal(signals, SignalKind.WindowFit).Normalized.Should().Be(1m);
        Signal(signals, SignalKind.WindowFit).Contribution.Should().Be(0.2m);
    }

    [Fact]
    public void Destination_affinity_saturates_at_three_confirmed_stays()
    {
        var signals = Score(nights: 14, visits: 3, historyTags: [OfferTag.Beach], creditShare: (120.75m, 48.30m, 60m));

        Signal(signals, SignalKind.DestinationAffinity).RawValue.Should().Be(3m);
        Signal(signals, SignalKind.DestinationAffinity).Normalized.Should().Be(1m);
    }

    [Fact]
    public void Tag_affinity_is_jaccard_similarity()
    {
        var signals = Score(nights: 14, visits: 1, historyTags: [OfferTag.Beach], creditShare: (120.75m, 48.30m, 60m));

        Signal(signals, SignalKind.TagAffinity).Normalized.Should().Be(0.5m);
    }

    [Fact]
    public void Credit_coverage_is_the_burn_capped_share_of_member_price()
    {
        var signals = Score(nights: 14, visits: 0, historyTags: [], creditShare: (120.75m, 48.30m, 60m));

        Signal(signals, SignalKind.CreditCoverage).Normalized.Should().Be(0.4m);
        Signal(signals, SignalKind.CreditCoverage).Contribution.Should().Be(0.08m);
    }

    [Fact]
    public void Price_drop_is_zero_without_a_watch()
    {
        var signals = Score(nights: 14, visits: 0, historyTags: [], creditShare: (120.75m, 48.30m, 60m));

        Signal(signals, SignalKind.PriceDrop).Normalized.Should().Be(0m);
    }

    [Fact]
    public void A_fifteen_percent_drop_against_a_ten_percent_threshold_is_half()
    {
        var offer = TravelOffer.Create(
            SupplierId.New(),
            "Coral Bay Resort",
            new Destination("MBJ", "Montego Bay"),
            Money.Of(85m, Currency.Usd),
            Money.Of(15m, Currency.Usd),
            [OfferTag.Beach, OfferTag.Family],
            4,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));
        var window = new TravelWindow(MemberId.New(), new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var history = new[] { new CompletedStay(offer.Destination, offer.Tags) };

        var signals = OpportunityScorer.Score(
            window,
            offer,
            Fixtures.Opportunities,
            history,
            Money.Of(120.75m, Currency.Usd),
            Money.Of(48.30m, Currency.Usd),
            Money.Of(60m, Currency.Usd),
            Money.Of(100m, Currency.Usd));

        Signal(signals, SignalKind.PriceDrop).RawValue.Should().Be(0.15m);
        Signal(signals, SignalKind.PriceDrop).Normalized.Should().Be(0.5m);
    }

    [Fact]
    public void Score_equals_the_sum_of_contributions()
    {
        var signals = Score(nights: 14, visits: 3, historyTags: [OfferTag.Beach, OfferTag.Family], creditShare: (120.75m, 48.30m, 60m));

        OpportunityScorer.Total(signals).Should().Be(signals.Sum(signal => signal.Contribution));
        OpportunityScorer.Total(signals).Should().Be(0.68m);
    }

    private static IReadOnlyList<OpportunitySignal> Score(
        int nights,
        int visits,
        OfferTag[] historyTags,
        (decimal Price, decimal Tender, decimal Balance) creditShare)
    {
        var offer = Fixtures.Offer(SupplierId.New());
        var start = new DateOnly(2026, 3, 29);
        var window = new TravelWindow(MemberId.New(), start, start.AddDays(nights));
        var history = Enumerable
            .Range(0, visits)
            .Select(_ => new CompletedStay(offer.Destination, historyTags.ToHashSet()))
            .ToArray();

        return OpportunityScorer.Score(
            window,
            offer,
            Fixtures.Opportunities,
            history,
            Money.Of(creditShare.Price, Currency.Usd),
            Money.Of(creditShare.Tender, Currency.Usd),
            Money.Of(creditShare.Balance, Currency.Usd),
            watchedBaselineNetRate: null);
    }

    private static OpportunitySignal Signal(IReadOnlyList<OpportunitySignal> signals, SignalKind kind) =>
        signals.Should().ContainSingle(signal => signal.Kind == kind).Subject;
}
