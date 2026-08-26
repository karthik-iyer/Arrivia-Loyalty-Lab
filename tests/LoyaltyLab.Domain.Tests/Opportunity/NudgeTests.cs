using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Opportunity;

public sealed class TravelWindowTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Nights_are_the_exclusive_end_minus_start()
    {
        var window = new TravelWindow(MemberId.New(), new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));

        window.Nights.Should().Be(14);
    }

    [Fact]
    public void Lead_days_use_the_injected_clock()
    {
        var window = new TravelWindow(MemberId.New(), new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));

        window.LeadDays(new MutableClock(AsOf)).Should().Be(14);
    }

    [Fact]
    public void A_reversed_range_is_rejected()
    {
        var act = () => new TravelWindow(MemberId.New(), new DateOnly(2026, 4, 12), new DateOnly(2026, 3, 29));

        act.Should().Throw<DomainException>();
    }
}

public sealed class OpportunitySignalTests
{
    [Fact]
    public void Contribution_is_normalized_times_weight()
    {
        var signal = OpportunitySignal.Of(SignalKind.WindowFit, rawValue: 14m, normalized: 1m, weight: 0.2m);

        signal.Contribution.Should().Be(0.2m);
    }

    [Fact]
    public void A_mismatched_contribution_is_rejected()
    {
        var act = () => new OpportunitySignal(SignalKind.PriceDrop, 0.15m, 1m, 0.2m, contribution: 0.99m);

        act.Should().Throw<DomainException>();
    }
}

public sealed class NudgeTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Delivered_nudge_persists_signals_and_a_rederivable_score()
    {
        var member = MemberId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var signals = new[]
        {
            OpportunitySignal.Of(SignalKind.WindowFit, 14m, 1m, 0.2m),
            OpportunitySignal.Of(SignalKind.DestinationAffinity, 1m, 0.5m, 0.2m),
            OpportunitySignal.Of(SignalKind.TagAffinity, 0.4m, 0.4m, 0.2m),
            OpportunitySignal.Of(SignalKind.CreditCoverage, 0.4m, 0.4m, 0.2m),
            OpportunitySignal.Of(SignalKind.PriceDrop, 0.12m, 0.4m, 0.2m),
        };

        var nudge = Nudge.Deliver(
            PartnerId.New(),
            member,
            OfferId.New(),
            window,
            signals,
            Fixtures.Opportunities,
            new MutableClock(AsOf));

        nudge.Status.Should().Be(NudgeStatus.Delivered);
        nudge.SuppressedBecause.Should().BeNull();
        nudge.Score.Should().Be(signals.Sum(signal => signal.Contribution));
        nudge.Score.Should().Be(0.54m);
        nudge.Signals.Should().HaveCount(5);
        nudge.ExpiresAt.Should().Be(AsOf.AddDays(7));
        nudge.IsExpired(new MutableClock(AsOf.AddDays(6))).Should().BeFalse();
        nudge.IsExpired(new MutableClock(AsOf.AddDays(7))).Should().BeTrue();
    }

    [Fact]
    public void Suppressed_nudge_records_the_reason_instead_of_dropping()
    {
        var member = MemberId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));

        var nudge = Nudge.Suppress(
            PartnerId.New(),
            member,
            window,
            SuppressionReason.FatigueCapReached,
            Fixtures.Opportunities,
            new MutableClock(AsOf));

        nudge.Status.Should().Be(NudgeStatus.Suppressed);
        nudge.SuppressedBecause.Should().Be(SuppressionReason.FatigueCapReached);
        nudge.OfferId.Should().BeNull();
        nudge.Signals.Should().BeEmpty();
    }

    [Fact]
    public void A_delivered_nudge_can_be_dismissed()
    {
        var member = MemberId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var nudge = Nudge.Deliver(
            PartnerId.New(),
            member,
            OfferId.New(),
            window,
            [OpportunitySignal.Of(SignalKind.WindowFit, 14m, 1m, 0.2m)],
            Fixtures.Opportunities,
            new MutableClock(AsOf));

        nudge.Dismiss();

        nudge.Status.Should().Be(NudgeStatus.Dismissed);
    }

    [Fact]
    public void A_delivered_nudge_can_be_actioned()
    {
        var nudge = Delivered();

        nudge.Action();

        nudge.Status.Should().Be(NudgeStatus.Actioned);
    }

    [Fact]
    public void A_delivered_nudge_can_expire()
    {
        var nudge = Delivered();

        nudge.Expire();

        nudge.Status.Should().Be(NudgeStatus.Expired);
    }

    [Fact]
    public void A_dismissed_nudge_cannot_be_actioned()
    {
        var nudge = Delivered();
        nudge.Dismiss();

        var act = () => nudge.Action();

        act.Should().Throw<DomainException>();
    }

    private static Nudge Delivered()
    {
        var member = MemberId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        return Nudge.Deliver(
            PartnerId.New(),
            member,
            OfferId.New(),
            window,
            [OpportunitySignal.Of(SignalKind.WindowFit, 14m, 1m, 0.2m)],
            Fixtures.Opportunities,
            new MutableClock(AsOf));
    }
}
