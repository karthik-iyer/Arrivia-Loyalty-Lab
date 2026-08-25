using LoyaltyLab.Api.Http;
using LoyaltyLab.Application.Concierge;
using LoyaltyLab.Domain.Concierge;

namespace LoyaltyLab.Api.Endpoints;

internal static class ConciergeEndpoints
{
    public static void MapConciergeEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/concierge/recommend", RecommendAsync);

    private static async Task<IResult> RecommendAsync(
        RecommendHttpRequest? body,
        Recommend recommend,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await recommend.ExecuteAsync(
            new RecommendCommand(body?.Text, body?.StayDate, body?.DestinationCode, body?.MaxBudget),
            cancellationToken);

        return result.Match(
            payload => Results.Ok(ConciergeRecommendHttp.From(payload)),
            error => ProblemResults.FromError(http, error));
    }
}

internal sealed record RecommendHttpRequest(
    string? Text,
    DateOnly? StayDate,
    string? DestinationCode,
    decimal? MaxBudget);

internal sealed record RecommendationItemHttp(
    Guid OfferId,
    string PropertyName,
    Guid QuoteId,
    MoneyHttp MemberPrice,
    int CreditsCover,
    decimal Score,
    IReadOnlyList<string> Reasons)
{
    public static RecommendationItemHttp From(RecommendedOffer item) =>
        new(
            item.OfferId.Value,
            item.PropertyName,
            item.QuoteId.Value,
            MoneyHttp.From(item.MemberPrice),
            item.CreditsCover,
            item.Score,
            item.Reasons);
}

internal sealed record ExclusionHttp(Guid OfferId, ExclusionReason Reason, string Detail)
{
    public static ExclusionHttp From(ExclusionRecord exclusion) =>
        new(exclusion.OfferId.Value, exclusion.Reason, exclusion.Detail);
}

internal sealed record RankingWeightsHttp(
    decimal ValueForMoney,
    decimal CreditCoverage,
    decimal TagMatch,
    decimal StarRating)
{
    public static RankingWeightsHttp From(RankingWeights weights) =>
        new(weights.ValueForMoney, weights.CreditCoverage, weights.TagMatch, weights.StarRating);
}

internal sealed record RecommendationAuditHttp(
    int CandidatesConsidered,
    int CandidatesReturned,
    IReadOnlyList<string> InterpretedTerms,
    IReadOnlyList<ExclusionHttp> Exclusions,
    RankingWeightsHttp Weights,
    bool NarrationApplied)
{
    public static RecommendationAuditHttp From(RecommendationAudit audit) =>
        new(
            audit.CandidatesConsidered,
            audit.CandidatesReturned,
            audit.InterpretedTerms,
            audit.Exclusions.Select(ExclusionHttp.From).ToArray(),
            RankingWeightsHttp.From(audit.Weights),
            audit.NarrationApplied);
}

internal sealed record ConciergeRecommendHttp(
    string Narrative,
    bool NarrationApplied,
    IReadOnlyList<RecommendationItemHttp> Recommendations,
    RecommendationAuditHttp Audit)
{
    public static ConciergeRecommendHttp From(RecommendResult result) =>
        new(
            result.Narrative,
            result.NarrationApplied,
            result.Recommendations.Select(RecommendationItemHttp.From).ToArray(),
            RecommendationAuditHttp.From(result.Audit));
}
