using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Concierge;

namespace LoyaltyLab.Domain.Tests.Concierge;

public sealed class CandidatePipelineTests
{
    private static readonly SupplierId Oceanic = SupplierId.New();
    private static readonly SupplierId Alpine = SupplierId.New();
    private static readonly DateOnly Stay = new(2026, 3, 15);
    private static readonly Destination Montego = new("MBJ", "Montego Bay");
    private static readonly Destination Zermatt = new("ZRH", "Zermatt");

    [Fact]
    public void Partner_excluded_supplier_never_ranks()
    {
        var coral = Beach("Coral Bay Resort");
        var matterhorn = Ski("Matterhorn Lodge");
        var result = Run(
            criteria: BeachInMarch(),
            catalog: [coral, matterhorn],
            permitted: [Oceanic],
            quotes: QuoteBoth(coral, matterhorn),
            credits: 6000);

        result.Recommendations.Should().ContainSingle(item => item.PropertyName == "Coral Bay Resort");
        result.Exclusions.Should().ContainSingle(item =>
            item.OfferId == matterhorn.Id && item.Reason == ExclusionReason.SupplierNotPermitted);
    }

    [Fact]
    public void Destination_mismatch_is_recorded_and_dropped()
    {
        var coral = Beach("Coral Bay Resort");
        var matterhorn = Ski("Matterhorn Lodge");
        var result = Run(
            BeachInMarch(),
            [coral, matterhorn],
            [Oceanic, Alpine],
            QuoteBoth(coral, matterhorn),
            credits: 6000);

        result.Recommendations.Select(item => item.PropertyName).Should().Equal("Coral Bay Resort");
        result.Exclusions.Should().Contain(item =>
            item.OfferId == matterhorn.Id && item.Reason == ExclusionReason.DestinationMismatch);
    }

    [Fact]
    public void Insufficient_credits_for_the_burn_capped_tender_excludes()
    {
        var coral = Beach("Coral Bay Resort");
        var villas = Beach("Blue Lagoon Villas", stars: 5);
        var quotes = new Dictionary<OfferId, PricedCandidate>
        {
            [coral.Id] = Priced(coral, 120.75m, 48.30m, 4830),
            [villas.Id] = Priced(villas, 270.00m, 108.00m, 10800),
        };

        var result = Run(BeachInMarch(), [coral, villas], [Oceanic], quotes, credits: 6000);

        result.Recommendations.Should().ContainSingle(item => item.OfferId == coral.Id);
        result.Exclusions.Should().ContainSingle(item =>
            item.OfferId == villas.Id
            && item.Reason == ExclusionReason.UnaffordableWithCredits
            && item.Detail.Contains("10800", StringComparison.Ordinal)
            && item.Detail.Contains("6000", StringComparison.Ordinal));
    }

    [Fact]
    public void Ranking_is_identical_across_repeated_runs_and_input_order()
    {
        var coral = Beach("Coral Bay Resort");
        var palms = Beach("Palms at Negril", stars: 3);
        var quotes = new Dictionary<OfferId, PricedCandidate>
        {
            [coral.Id] = Priced(coral, 120.75m, 48.30m, 4830),
            [palms.Id] = Priced(palms, 95.00m, 38.00m, 3800),
        };

        var first = Run(BeachInMarch(), [coral, palms], [Oceanic], quotes, 6000);
        var second = Run(BeachInMarch(), [palms, coral], [Oceanic], quotes, 6000);
        var third = Run(BeachInMarch(), [coral, palms], [Oceanic], quotes, 6000);

        first.Recommendations.Select(item => (item.OfferId, item.Score))
            .Should().Equal(second.Recommendations.Select(item => (item.OfferId, item.Score)));
        first.Recommendations.Should().BeEquivalentTo(third.Recommendations, options => options.WithStrictOrdering());
        first.Recommendations.Should().NotBeEmpty();
        first.Recommendations.All(item => item.OfferId == coral.Id || item.OfferId == palms.Id).Should().BeTrue();
    }

    [Fact]
    public void Cheaper_offer_outranks_when_other_signals_tie()
    {
        var coral = Beach("Coral Bay Resort");
        var palms = Beach("Palms at Negril", stars: 4);
        var quotes = new Dictionary<OfferId, PricedCandidate>
        {
            [coral.Id] = Priced(coral, 120.75m, 48.30m, 4830),
            [palms.Id] = Priced(palms, 95.00m, 38.00m, 3800),
        };

        var ranked = Run(BeachInMarch(), [coral, palms], [Oceanic], quotes, 6000).Recommendations;

        ranked[0].OfferId.Should().Be(palms.Id);
        ranked[0].Score.Should().BeGreaterThan(ranked[1].Score);
    }

    [Fact]
    public void Missing_quote_is_tier_exclusion()
    {
        var coral = Beach("Coral Bay Resort");
        var result = Run(
            BeachInMarch(),
            [coral],
            [Oceanic],
            new Dictionary<OfferId, PricedCandidate>(),
            6000);

        result.Recommendations.Should().BeEmpty();
        result.Exclusions.Should().ContainSingle(item => item.Reason == ExclusionReason.TierNotEntitled);
    }

    [Fact]
    public void Budget_ceiling_excludes_over_budget_quotes()
    {
        var coral = Beach("Coral Bay Resort");
        var criteria = BeachInMarch() with { MaxBudget = Money.Of(100m, Currency.Usd) };
        var quotes = new Dictionary<OfferId, PricedCandidate>
        {
            [coral.Id] = Priced(coral, 120.75m, 48.30m, 4830),
        };

        var result = Run(criteria, [coral], [Oceanic], quotes, 6000);

        result.Exclusions.Should().ContainSingle(item => item.Reason == ExclusionReason.BudgetExceeded);
    }

    [Fact]
    public void Stay_outside_availability_is_excluded()
    {
        var winterOnly = TravelOffer.Create(
            Oceanic,
            "Coral Bay Resort",
            Montego,
            Money.Of(100m, Currency.Usd),
            Money.Of(15m, Currency.Usd),
            [OfferTag.Beach],
            4,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 28));
        var quotes = new Dictionary<OfferId, PricedCandidate>
        {
            [winterOnly.Id] = Priced(winterOnly, 120.75m, 48.30m, 4830),
        };

        var result = Run(BeachInMarch(), [winterOnly], [Oceanic], quotes, 6000);

        result.Exclusions.Should().ContainSingle(item => item.Reason == ExclusionReason.OutsideAvailability);
    }

    private static RecommendationSet Run(
        RecommendationCriteria criteria,
        IReadOnlyList<TravelOffer> catalog,
        HashSet<SupplierId> permitted,
        IReadOnlyDictionary<OfferId, PricedCandidate> quotes,
        int credits) =>
        CandidatePipeline.Evaluate(
            new RecommendationRequest(criteria, ["beach", "March"], catalog, permitted, quotes, credits));

    private static RecommendationCriteria BeachInMarch() =>
        new("MBJ", new HashSet<OfferTag> { OfferTag.Beach }, Stay, MaxBudget: null);

    private static TravelOffer Beach(string name, int stars = 4) =>
        TravelOffer.Create(
            Oceanic,
            name,
            Montego,
            Money.Of(100m, Currency.Usd),
            Money.Of(15m, Currency.Usd),
            [OfferTag.Beach],
            stars,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));

    private static TravelOffer Ski(string name) =>
        TravelOffer.Create(
            Alpine,
            name,
            Zermatt,
            Money.Of(180m, Currency.Usd),
            Money.Of(22m, Currency.Usd),
            [OfferTag.Ski],
            4,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));

    private static Dictionary<OfferId, PricedCandidate> QuoteBoth(TravelOffer left, TravelOffer right) =>
        new()
        {
            [left.Id] = Priced(left, 120.75m, 48.30m, 4830),
            [right.Id] = Priced(right, 210.00m, 84.00m, 8400),
        };

    private static PricedCandidate Priced(TravelOffer offer, decimal price, decimal tender, int credits) =>
        new(offer, Money.Of(price, Currency.Usd), Money.Of(tender, Currency.Usd), credits);
}
