using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Opportunity;

public enum NudgeStatus
{
    Pending = 0,
    Delivered = 1,
    Actioned = 2,
    Dismissed = 3,
    Expired = 4,
    Suppressed = 5,
}

public enum SuppressionReason
{
    FatigueCapReached = 0,
    CooldownActive = 1,
    ScoreBelowThreshold = 2,
    NoEligibleInventory = 3,
    WindowTooSoon = 4,
    DuplicateOfRecentNudge = 5,
}

/// <summary>
/// A persisted opportunity, including deliberate silences (FR-O-05, requirements §6.2).
/// </summary>
public sealed class Nudge : Entity<NudgeId>, ITenantOwned
{
    private Nudge()
    {
        Signals = [];
    }

    private Nudge(
        NudgeId id,
        PartnerId partnerId,
        MemberId memberId,
        OfferId? offerId,
        DateOnly windowStart,
        DateOnly windowEnd,
        decimal score,
        List<OpportunitySignal> signals,
        NudgeStatus status,
        SuppressionReason? suppressedBecause,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
        : base(id)
    {
        PartnerId = partnerId;
        MemberId = memberId;
        OfferId = offerId;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
        Score = score;
        Signals = signals;
        Status = status;
        SuppressedBecause = suppressedBecause;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public PartnerId PartnerId { get; private set; }

    public MemberId MemberId { get; private set; }

    public OfferId? OfferId { get; private set; }

    public DateOnly WindowStart { get; private set; }

    public DateOnly WindowEnd { get; private set; }

    public decimal Score { get; private set; }

    public List<OpportunitySignal> Signals { get; private set; }

    public NudgeStatus Status { get; private set; }

    public SuppressionReason? SuppressedBecause { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsExpired(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return clock.UtcNow >= ExpiresAt;
    }

    public void Dismiss()
    {
        EnsureDelivered("dismissed");
        Status = NudgeStatus.Dismissed;
    }

    public void Action()
    {
        EnsureDelivered("actioned");
        Status = NudgeStatus.Actioned;
    }

    public void Expire()
    {
        EnsureDelivered("expired");
        Status = NudgeStatus.Expired;
    }

    private void EnsureDelivered(string verb)
    {
        if (Status != NudgeStatus.Delivered)
        {
            throw new DomainException($"Only a delivered nudge can be {verb}.");
        }
    }

    public static Nudge Deliver(
        PartnerId partnerId,
        MemberId memberId,
        OfferId offerId,
        TravelWindow window,
        IReadOnlyList<OpportunitySignal> signals,
        OpportunityPolicy policy,
        IClock clock,
        NudgeId? id = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        if (window.MemberId != memberId)
        {
            throw new DomainException("A nudge window must belong to the same member.");
        }

        if (signals.Count == 0)
        {
            throw new DomainException("A delivered nudge must carry its trigger signals.");
        }

        var score = signals.Sum(signal => signal.Contribution);
        var created = clock.UtcNow;
        return new Nudge(
            id ?? NudgeId.New(),
            partnerId,
            memberId,
            offerId,
            window.Start,
            window.End,
            score,
            [.. signals],
            NudgeStatus.Delivered,
            suppressedBecause: null,
            created,
            created.AddDays(policy.NudgeLifetimeDays));
    }

    public static Nudge Suppress(
        PartnerId partnerId,
        MemberId memberId,
        TravelWindow window,
        SuppressionReason reason,
        OpportunityPolicy policy,
        IClock clock,
        OfferId? offerId = null,
        IReadOnlyList<OpportunitySignal>? signals = null,
        NudgeId? id = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        if (window.MemberId != memberId)
        {
            throw new DomainException("A nudge window must belong to the same member.");
        }

        var recorded = signals is null ? [] : signals.ToList();
        var score = recorded.Sum(signal => signal.Contribution);
        var created = clock.UtcNow;
        return new Nudge(
            id ?? NudgeId.New(),
            partnerId,
            memberId,
            offerId,
            window.Start,
            window.End,
            score,
            recorded,
            NudgeStatus.Suppressed,
            reason,
            created,
            created.AddDays(policy.NudgeLifetimeDays));
    }
}
