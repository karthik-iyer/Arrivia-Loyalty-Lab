using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Concierge;

namespace LoyaltyLab.Domain.Tests.Concierge;

public sealed class RecommendationAuditTests
{
    [Fact]
    public void Every_catalog_row_is_either_returned_or_excluded_with_a_reason()
    {
        var oceanic = SupplierId.New();
        var alpine = SupplierId.New();
        var coral = Offer(oceanic, "Coral Bay Resort", "MBJ", "Montego Bay", OfferTag.Beach);
        var matterhorn = Offer(alpine, "Matterhorn Lodge", "ZRH", "Zermatt", OfferTag.Ski);
        var winterOnly = TravelOffer.Create(
            oceanic,
            "Closed Palms",
            new Destination("MBJ", "Montego Bay"),
            Money.Of(80m, Currency.Usd),
            Money.Of(10m, Currency.Usd),
            [OfferTag.Beach],
            3,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1));

        var catalog = new[] { coral, matterhorn, winterOnly };
        var quotes = new Dictionary<OfferId, PricedCandidate>
        {
            [coral.Id] = new(coral, Money.Of(120.75m, Currency.Usd), Money.Of(48.30m, Currency.Usd), 4830),
            [matterhorn.Id] = new(matterhorn, Money.Of(210m, Currency.Usd), Money.Of(84m, Currency.Usd), 8400),
            [winterOnly.Id] = new(winterOnly, Money.Of(90m, Currency.Usd), Money.Of(36m, Currency.Usd), 3600),
        };

        var result = CandidatePipeline.Evaluate(
            new RecommendationRequest(
                new RecommendationCriteria("MBJ", new HashSet<OfferTag> { OfferTag.Beach }, new DateOnly(2026, 3, 15), null),
                ["beach", "March"],
                catalog,
                new HashSet<SupplierId> { oceanic, alpine },
                quotes,
                CreditBalance: 6000));

        var audit = result.Audit;
        audit.CandidatesConsidered.Should().Be(catalog.Length);
        audit.CandidatesReturned.Should().Be(result.Recommendations.Count);
        (audit.CandidatesReturned + audit.Exclusions.Count).Should().Be(audit.CandidatesConsidered);
        audit.Exclusions.Should().NotBeEmpty();
        audit.Exclusions.Should().OnlyContain(item =>
            !string.IsNullOrWhiteSpace(item.Detail) && Enum.IsDefined(item.Reason));
        audit.InterpretedTerms.Should().Equal("beach", "March");
        audit.Weights.Should().Be(RankingWeights.Default);
        audit.NarrationApplied.Should().BeFalse();
        result.Recommendations.Should().OnlyContain(item => catalog.Any(offer => offer.Id == item.OfferId));
    }

    private static TravelOffer Offer(
        SupplierId supplier,
        string name,
        string code,
        string city,
        OfferTag tag) =>
        TravelOffer.Create(
            supplier,
            name,
            new Destination(code, city),
            Money.Of(100m, Currency.Usd),
            Money.Of(15m, Currency.Usd),
            [tag],
            4,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));
}
