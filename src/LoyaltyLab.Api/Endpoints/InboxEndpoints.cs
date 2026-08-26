using LoyaltyLab.Api.Http;
using LoyaltyLab.Application.Opportunity;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;

namespace LoyaltyLab.Api.Endpoints;

internal static class InboxEndpoints
{
    public static void MapInboxEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/inbox", ListAsync);
        app.MapPost("/api/inbox/{nudgeId:guid}/action", ActionAsync);
        app.MapPost("/api/inbox/{nudgeId:guid}/dismiss", DismissAsync);
    }

    private static async Task<IResult> ListAsync(
        GetInbox inbox,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await inbox.ExecuteAsync(new GetInboxQuery(), cancellationToken);
        return result.Match(
            payload => Results.Ok(InboxHttp.From(payload)),
            error => ProblemResults.FromError(http, error));
    }

    private static async Task<IResult> ActionAsync(
        Guid nudgeId,
        ActionNudge action,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await action.ExecuteAsync(new ActionNudgeCommand(new NudgeId(nudgeId)), cancellationToken);
        return result.Match(
            payload => Results.Ok(ActionedNudgeHttp.From(payload)),
            error => ProblemResults.FromError(http, error));
    }

    private static async Task<IResult> DismissAsync(
        Guid nudgeId,
        DismissNudge dismiss,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await dismiss.ExecuteAsync(new DismissNudgeCommand(new NudgeId(nudgeId)), cancellationToken);
        return result.Match(
            payload => Results.Ok(DismissedNudgeHttp.From(payload)),
            error => ProblemResults.FromError(http, error));
    }
}

internal sealed record OpportunitySignalHttp(
    SignalKind Kind,
    decimal RawValue,
    decimal Normalized,
    decimal Weight,
    decimal Contribution)
{
    public static OpportunitySignalHttp From(OpportunitySignal signal) =>
        new(signal.Kind, signal.RawValue, signal.Normalized, signal.Weight, signal.Contribution);
}

internal sealed record InboxNudgeHttp(
    Guid NudgeId,
    Guid OfferId,
    string PropertyName,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    decimal Score,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<OpportunitySignalHttp> Signals)
{
    public static InboxNudgeHttp From(InboxNudge nudge) =>
        new(
            nudge.Id.Value,
            nudge.OfferId.Value,
            nudge.PropertyName,
            nudge.WindowStart,
            nudge.WindowEnd,
            nudge.Score,
            nudge.ExpiresAt,
            nudge.Signals.Select(OpportunitySignalHttp.From).ToArray());
}

internal sealed record InboxHttp(IReadOnlyList<InboxNudgeHttp> Nudges)
{
    public static InboxHttp From(GetInboxResult inbox) =>
        new(inbox.Nudges.Select(InboxNudgeHttp.From).ToArray());
}

internal sealed record ActionedNudgeHttp(
    Guid NudgeId,
    Guid QuoteId,
    Guid OfferId,
    MoneyHttp MemberPrice,
    MoneyHttp MaxCreditTender,
    int MaxCredits,
    DateTimeOffset ExpiresAt)
{
    public static ActionedNudgeHttp From(ActionNudgeResult actioned) =>
        new(
            actioned.NudgeId.Value,
            actioned.QuoteId.Value,
            actioned.OfferId.Value,
            MoneyHttp.From(actioned.MemberPrice),
            MoneyHttp.From(actioned.MaxCreditTender),
            actioned.MaxCredits,
            actioned.ExpiresAt);
}

internal sealed record DismissedNudgeHttp(Guid NudgeId, NudgeStatus Status)
{
    public static DismissedNudgeHttp From(DismissNudgeResult dismissed) =>
        new(dismissed.NudgeId.Value, dismissed.Status);
}
