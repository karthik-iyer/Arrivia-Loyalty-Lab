using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Concierge;

namespace LoyaltyLab.Domain.Tests.Concierge;

public sealed class NarrationValidatorTests
{
    [Fact]
    public void Template_is_grounded_in_the_facts()
    {
        var facts = Facts("Coral Bay Resort", 120.75m);
        var template = NarrationTemplate.Render(facts);

        template.Should().Contain("Coral Bay Resort");
        template.Should().NotContain("$");
        NarrationValidator.IsGrounded(template, facts).Should().BeTrue();
    }

    [Fact]
    public void Empty_result_template_has_no_property_or_price()
    {
        var facts = Facts();
        var template = NarrationTemplate.Render(facts);

        template.Should().Be("No stays fit those dates and credits.");
        NarrationValidator.IsGrounded(template, facts).Should().BeTrue();
    }

    [Fact]
    public void Invented_price_is_rejected()
    {
        var facts = Facts("Coral Bay Resort", 120.75m);

        NarrationValidator
            .IsGrounded("Coral Bay Resort is a bargain at $9.99.", facts)
            .Should().BeFalse();
    }

    [Fact]
    public void Invented_property_is_rejected()
    {
        var facts = Facts("Coral Bay Resort", 120.75m);

        NarrationValidator
            .IsGrounded("Atlantis Resort is $120.75.", facts)
            .Should().BeFalse();
    }

    [Fact]
    public void Stated_price_and_name_from_the_facts_are_accepted()
    {
        var facts = Facts("Coral Bay Resort", 120.75m);

        NarrationValidator
            .IsGrounded("Coral Bay Resort comes to $120.75.", facts)
            .Should().BeTrue();
    }

    private static RecommendationSet Facts(string? property = null, decimal? price = null)
    {
        var recommendations = new List<RankedRecommendation>();
        if (property is not null && price is not null)
        {
            recommendations.Add(
                new RankedRecommendation(
                    OfferId.New(),
                    property,
                    Money.Of(price.Value, Currency.Usd),
                    4830,
                    0.82m,
                    ["Matches: beach"]));
        }

        return new RecommendationSet(
            recommendations,
            new RecommendationAudit(
                CandidatesConsidered: 4,
                CandidatesReturned: recommendations.Count,
                Exclusions: [],
                InterpretedTerms: ["beach"],
                RankingWeights.Default,
                NarrationApplied: false));
    }
}
