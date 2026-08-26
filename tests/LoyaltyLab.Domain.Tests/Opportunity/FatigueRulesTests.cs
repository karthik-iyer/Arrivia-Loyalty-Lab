using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Opportunity;

public sealed class FatigueRulesTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Cooldown_wins_when_a_similar_nudge_was_dismissed()
    {
        var member = MemberId.New();
        var offer = OfferId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var dismissed = Deliver(member, offer, window);
        dismissed.Dismiss();

        var reason = FatigueRules.FirstMatch(
            offer,
            window,
            [dismissed],
            Fixtures.Opportunities,
            new MutableClock(AsOf));

        reason.Should().Be(SuppressionReason.CooldownActive);
    }

    [Fact]
    public void Cap_suppresses_after_the_weekly_delivered_limit()
    {
        var member = MemberId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var prior = new[]
        {
            Deliver(member, OfferId.New(), window),
            Deliver(member, OfferId.New(), window),
        };

        var reason = FatigueRules.FirstMatch(
            OfferId.New(),
            window,
            prior,
            Fixtures.Opportunities,
            new MutableClock(AsOf));

        reason.Should().Be(SuppressionReason.FatigueCapReached);
    }

    [Fact]
    public void A_matching_offer_and_overlapping_window_is_a_duplicate()
    {
        var member = MemberId.New();
        var offer = OfferId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var prior = Deliver(member, offer, window);

        var reason = FatigueRules.FirstMatch(
            offer,
            window,
            [prior],
            Fixtures.Opportunities,
            new MutableClock(AsOf));

        reason.Should().Be(SuppressionReason.DuplicateOfRecentNudge);
    }

    [Fact]
    public void Cooldown_is_checked_before_the_cap()
    {
        var member = MemberId.New();
        var offer = OfferId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var first = Deliver(member, OfferId.New(), window);
        var second = Deliver(member, offer, window);
        second.Dismiss();

        var reason = FatigueRules.FirstMatch(
            offer,
            window,
            [first, second],
            Fixtures.Opportunities,
            new MutableClock(AsOf));

        reason.Should().Be(SuppressionReason.CooldownActive);
    }

    [Fact]
    public void No_prior_contact_leaves_the_member_eligible()
    {
        var member = MemberId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));

        var reason = FatigueRules.FirstMatch(
            OfferId.New(),
            window,
            [],
            Fixtures.Opportunities,
            new MutableClock(AsOf));

        reason.Should().BeNull();
    }

    private static Nudge Deliver(MemberId member, OfferId offer, TravelWindow window) =>
        Nudge.Deliver(
            PartnerId.New(),
            member,
            offer,
            window,
            [OpportunitySignal.Of(SignalKind.WindowFit, 14m, 1m, 1m)],
            Fixtures.Opportunities,
            new MutableClock(AsOf));
}
