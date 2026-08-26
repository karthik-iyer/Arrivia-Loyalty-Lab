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

public sealed record ScanOpportunitiesCommand(int WatchBatchSize = 10);

public sealed record ScanOpportunitiesResult(int MembersScanned, int WatchesRefreshed, int NudgesWritten);

public sealed record GetInboxQuery;

public sealed record InboxNudge(
    NudgeId Id,
    OfferId OfferId,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    decimal Score,
    IReadOnlyList<OpportunitySignal> Signals,
    DateTimeOffset ExpiresAt)
{
    public static InboxNudge From(Nudge nudge)
    {
        ArgumentNullException.ThrowIfNull(nudge);
        if (nudge.OfferId is not { } offerId)
        {
            throw new DomainException("A delivered nudge must name an offer.");
        }

        return new(
            nudge.Id,
            offerId,
            nudge.WindowStart,
            nudge.WindowEnd,
            nudge.Score,
            nudge.Signals,
            nudge.ExpiresAt);
    }
}

public sealed record GetInboxResult(IReadOnlyList<InboxNudge> Nudges);

public sealed record ActionNudgeCommand(NudgeId NudgeId);

public sealed record ActionNudgeResult(
    NudgeId NudgeId,
    QuoteId QuoteId,
    OfferId OfferId,
    Money MemberPrice,
    Money MaxCreditTender,
    int MaxCredits,
    DateTimeOffset ExpiresAt);

public sealed record DismissNudgeCommand(NudgeId NudgeId);

public sealed record DismissNudgeResult(NudgeId NudgeId, NudgeStatus Status);
