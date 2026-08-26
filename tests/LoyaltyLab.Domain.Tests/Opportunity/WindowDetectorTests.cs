using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Opportunity;

public sealed class WindowDetectorTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Mayas_seeded_busy_periods_leave_a_fourteen_night_gap()
    {
        var member = MemberId.New();
        var partner = PartnerId.New();
        var busy = new[]
        {
            BusyPeriod.Create(partner, member, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 29)),
            BusyPeriod.Create(partner, member, new DateOnly(2026, 4, 12), new DateOnly(2026, 5, 1)),
        };

        var windows = WindowDetector.Detect(member, busy, Fixtures.Opportunities, new MutableClock(AsOf));

        var window = windows.Should().ContainSingle().Subject;
        window.Start.Should().Be(new DateOnly(2026, 3, 29));
        window.End.Should().Be(new DateOnly(2026, 4, 12));
        window.Nights.Should().Be(14);
        window.LeadDays(new MutableClock(AsOf)).Should().Be(14);
    }

    [Fact]
    public void Gaps_shorter_than_the_minimum_are_dropped()
    {
        var member = MemberId.New();
        var partner = PartnerId.New();
        var busy = new[]
        {
            BusyPeriod.Create(partner, member, new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 29)),
            BusyPeriod.Create(partner, member, new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 20)),
        };

        var windows = WindowDetector.Detect(member, busy, Fixtures.Opportunities, new MutableClock(AsOf));

        windows.Should().BeEmpty();
    }

    [Fact]
    public void Gaps_inside_the_minimum_lead_time_are_dropped()
    {
        var member = MemberId.New();
        var partner = PartnerId.New();
        var busy = new[]
        {
            BusyPeriod.Create(partner, member, new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 22)),
            BusyPeriod.Create(partner, member, new DateOnly(2026, 4, 6), new DateOnly(2026, 4, 20)),
        };

        var windows = WindowDetector.Detect(member, busy, Fixtures.Opportunities, new MutableClock(AsOf));

        windows.Should().BeEmpty();
    }

    [Fact]
    public void Overlapping_busy_periods_merge_before_gaps_are_taken()
    {
        var member = MemberId.New();
        var partner = PartnerId.New();
        var busy = new[]
        {
            BusyPeriod.Create(partner, member, new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 1)),
            BusyPeriod.Create(partner, member, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 20)),
            BusyPeriod.Create(partner, member, new DateOnly(2026, 4, 12), new DateOnly(2026, 5, 1)),
        };

        var windows = WindowDetector.Detect(member, busy, Fixtures.Opportunities, new MutableClock(AsOf));

        var window = windows.Should().ContainSingle().Subject;
        window.Start.Should().Be(new DateOnly(2026, 4, 1));
        window.End.Should().Be(new DateOnly(2026, 4, 12));
    }

    [Fact]
    public void An_empty_calendar_does_not_qualify_when_lead_time_starts_today()
    {
        var member = MemberId.New();

        var windows = WindowDetector.Detect(member, [], Fixtures.Opportunities, new MutableClock(AsOf));

        windows.Should().BeEmpty();
    }
}
