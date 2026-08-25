using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Opportunity;

/// <summary>
/// A gap in a member's calendar long enough to be a plausible trip (FR-O-01).
/// <see cref="End"/> is the exclusive checkout date, so <see cref="Nights"/> is End − Start.
/// </summary>
public sealed record TravelWindow
{
    public TravelWindow(MemberId memberId, DateOnly start, DateOnly end)
    {
        if (end <= start)
        {
            throw new DomainException("A travel window needs a checkout date after the arrival date.");
        }

        MemberId = memberId;
        Start = start;
        End = end;
    }

    public MemberId MemberId { get; }

    public DateOnly Start { get; }

    public DateOnly End { get; }

    public int Nights => End.DayNumber - Start.DayNumber;

    public int LeadDays(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        return Start.DayNumber - today.DayNumber;
    }
}
