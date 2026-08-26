using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Opportunity;

/// <summary>
/// Fatigue is evaluated in this order; the first match suppresses (docs/04 §6.3, FR-O-06).
/// </summary>
public static class FatigueRules
{
    public const int TrailingWeekDays = 7;

    public static SuppressionReason? FirstMatch(
        OfferId offerId,
        TravelWindow window,
        IReadOnlyList<Nudge> prior,
        OpportunityPolicy policy,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(prior);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        if (OnCooldown(offerId, window, prior, policy, clock))
        {
            return SuppressionReason.CooldownActive;
        }

        if (CapReached(prior, policy, clock))
        {
            return SuppressionReason.FatigueCapReached;
        }

        if (IsDuplicate(offerId, window, prior))
        {
            return SuppressionReason.DuplicateOfRecentNudge;
        }

        return null;
    }

    private static bool OnCooldown(
        OfferId offerId,
        TravelWindow window,
        IReadOnlyList<Nudge> prior,
        OpportunityPolicy policy,
        IClock clock)
    {
        var cutoff = clock.UtcNow.AddDays(-policy.DismissalCooldownDays);
        return prior.Any(nudge =>
            nudge.Status == NudgeStatus.Dismissed
            && nudge.CreatedAt >= cutoff
            && IsSimilar(nudge, offerId, window));
    }

    private static bool CapReached(IReadOnlyList<Nudge> prior, OpportunityPolicy policy, IClock clock)
    {
        var cutoff = clock.UtcNow.AddDays(-TrailingWeekDays);
        var sent = prior.Count(nudge => WasSent(nudge) && nudge.CreatedAt >= cutoff);
        return sent >= policy.MaxNudgesPerMemberPerWeek;
    }

    private static bool IsDuplicate(OfferId offerId, TravelWindow window, IReadOnlyList<Nudge> prior) =>
        prior.Any(nudge =>
            WasSent(nudge)
            && nudge.OfferId == offerId
            && WindowsOverlap(nudge.WindowStart, nudge.WindowEnd, window.Start, window.End));

    private static bool IsSimilar(Nudge nudge, OfferId offerId, TravelWindow window) =>
        nudge.OfferId == offerId
        || WindowsOverlap(nudge.WindowStart, nudge.WindowEnd, window.Start, window.End);

    private static bool WasSent(Nudge nudge) =>
        nudge.Status is NudgeStatus.Delivered or NudgeStatus.Actioned or NudgeStatus.Dismissed or NudgeStatus.Expired;

    private static bool WindowsOverlap(DateOnly leftStart, DateOnly leftEnd, DateOnly rightStart, DateOnly rightEnd) =>
        leftStart < rightEnd && rightStart < leftEnd;
}
