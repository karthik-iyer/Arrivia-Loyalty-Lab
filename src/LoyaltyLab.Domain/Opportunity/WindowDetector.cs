using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Opportunity;

/// <summary>
/// Travel windows are gaps on the seeded availability feed (docs/04 §6.1, FR-O-01).
/// Search is bounded by <see cref="LookAheadDays"/> from today; gaps after the last
/// busy period are not extended into an open-ended future.
/// </summary>
public static class WindowDetector
{
    public const int LookAheadDays = 180;

    public static IReadOnlyList<TravelWindow> Detect(
        MemberId memberId,
        IReadOnlyList<BusyPeriod> busy,
        OpportunityPolicy policy,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(busy);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        return Gaps(memberId, busy, clock)
            .Where(window => window.Nights >= policy.MinWindowNights)
            .Where(window => window.LeadDays(clock) >= policy.MinLeadDays)
            .ToArray();
    }

    private static List<TravelWindow> Gaps(
        MemberId memberId,
        IReadOnlyList<BusyPeriod> busy,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(busy);
        ArgumentNullException.ThrowIfNull(clock);

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var horizon = today.AddDays(LookAheadDays);
        var merged = Merge(busy.Where(period => period.MemberId == memberId));

        if (merged.Count == 0)
        {
            return horizon > today ? [new TravelWindow(memberId, today, horizon)] : [];
        }

        var windows = new List<TravelWindow>();
        var cursor = today;
        foreach (var period in merged)
        {
            if (period.End <= cursor)
            {
                continue;
            }

            var gapEnd = period.Start < horizon ? period.Start : horizon;
            if (gapEnd > cursor)
            {
                windows.Add(new TravelWindow(memberId, cursor, gapEnd));
            }

            if (period.End > cursor)
            {
                cursor = period.End;
            }

            if (cursor >= horizon)
            {
                break;
            }
        }

        return windows;
    }

    private static List<BusyPeriod> Merge(IEnumerable<BusyPeriod> busy)
    {
        var merged = new List<BusyPeriod>();
        foreach (var period in busy.OrderBy(item => item.Start.DayNumber).ThenBy(item => item.End.DayNumber))
        {
            if (merged.Count == 0 || merged[^1].End < period.Start)
            {
                merged.Add(period);
                continue;
            }

            var last = merged[^1];
            if (period.End > last.End)
            {
                merged[^1] = BusyPeriod.Create(last.PartnerId, last.MemberId, last.Start, period.End, last.Id);
            }
        }

        return merged;
    }
}
