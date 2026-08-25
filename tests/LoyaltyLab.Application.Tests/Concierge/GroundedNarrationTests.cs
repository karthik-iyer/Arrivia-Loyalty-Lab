using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Concierge;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Concierge;

namespace LoyaltyLab.Application.Tests.Concierge;

public sealed class GroundedNarrationTests
{
    [Fact]
    public async Task Null_narrator_returns_the_template_and_does_not_mark_applied()
    {
        var facts = CoralFacts();
        var outcome = await GroundedNarration.ApplyAsync(new NullOfferNarrator(), facts, CancellationToken.None);

        outcome.Narrative.Should().Be(NarrationTemplate.Render(facts));
        outcome.Audit.NarrationApplied.Should().BeFalse();
    }

    [Fact]
    public async Task Invented_price_falls_back_to_the_template()
    {
        var facts = CoralFacts();
        var outcome = await GroundedNarration.ApplyAsync(
            new ScriptedNarrator("Coral Bay Resort from $9.99 a night."),
            facts,
            CancellationToken.None);

        outcome.Narrative.Should().Be(NarrationTemplate.Render(facts));
        outcome.Narrative.Should().NotContain("$9.99");
        outcome.Audit.NarrationApplied.Should().BeFalse();
    }

    [Fact]
    public async Task Grounded_rephrase_is_kept_and_marked_applied()
    {
        var facts = CoralFacts();
        const string prose = "Coral Bay Resort comes to $120.75 and uses your credits well.";
        var outcome = await GroundedNarration.ApplyAsync(new ScriptedNarrator(prose), facts, CancellationToken.None);

        outcome.Narrative.Should().Be(prose);
        outcome.Audit.NarrationApplied.Should().BeTrue();
    }

    [Fact]
    public async Task Narrator_failure_falls_back_to_the_template()
    {
        var facts = CoralFacts();
        var outcome = await GroundedNarration.ApplyAsync(new FailingNarrator(), facts, CancellationToken.None);

        outcome.Narrative.Should().Be(NarrationTemplate.Render(facts));
        outcome.Audit.NarrationApplied.Should().BeFalse();
    }

    private static RecommendationSet CoralFacts() =>
        new(
            [
                new RankedRecommendation(
                    OfferId.New(),
                    "Coral Bay Resort",
                    Money.Of(120.75m, Currency.Usd),
                    4830,
                    0.82m,
                    ["Matches: beach"]),
            ],
            new RecommendationAudit(4, 1, [], ["beach"], RankingWeights.Default, NarrationApplied: false));

    private sealed class ScriptedNarrator(string prose) : IOfferNarrator
    {
        public Task<Result<string>> NarrateAsync(RecommendationSet facts, CancellationToken cancellationToken) =>
            Task.FromResult(Result<string>.Success(prose));
    }

    private sealed class FailingNarrator : IOfferNarrator
    {
        public Task<Result<string>> NarrateAsync(RecommendationSet facts, CancellationToken cancellationToken) =>
            Task.FromResult(Result<string>.Failure(Errors.TemporaryFailure));
    }
}
