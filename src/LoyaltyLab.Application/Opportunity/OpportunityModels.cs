using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;

namespace LoyaltyLab.Application.Opportunity;

public sealed record DetectTravelWindowsQuery;

public sealed record DetectTravelWindowsResult(IReadOnlyList<TravelWindow> Windows);

public sealed record EvaluateOpportunitiesCommand;

public sealed record EvaluatedNudge(
    NudgeId Id,
    NudgeStatus Status,
    SuppressionReason? SuppressedBecause,
    OfferId? OfferId,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    decimal Score,
    IReadOnlyList<OpportunitySignal> Signals)
{
    public static EvaluatedNudge From(Nudge nudge)
    {
        ArgumentNullException.ThrowIfNull(nudge);
        return new(
            nudge.Id,
            nudge.Status,
            nudge.SuppressedBecause,
            nudge.OfferId,
            nudge.WindowStart,
            nudge.WindowEnd,
            nudge.Score,
            nudge.Signals);
    }
}

public sealed record EvaluateOpportunitiesResult(IReadOnlyList<EvaluatedNudge> Nudges);
