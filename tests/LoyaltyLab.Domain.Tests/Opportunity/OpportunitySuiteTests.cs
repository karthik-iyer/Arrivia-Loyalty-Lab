using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Opportunity;

/// <summary>
/// T-076: the named opportunity properties that earlier tasks only proved in isolation (G15, G16).
/// </summary>
public sealed class OpportunitySuiteTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Maya_gap_is_detected_only_when_lead_and_nights_meet_policy()
    {
        var member = MemberId.New();
        var partner = PartnerId.New();
        var busy = MayaBusy(partner, member);
        var clock = new MutableClock(AsOf);

        var found = WindowDetector.Detect(member, busy, Policy(), clock).Should().ContainSingle().Subject;
        found.Start.Should().Be(new DateOnly(2026, 3, 29));
        found.End.Should().Be(new DateOnly(2026, 4, 12));
        found.Nights.Should().Be(14);
        found.LeadDays(clock).Should().Be(14);

        WindowDetector.Detect(member, busy, Policy(minLeadDays: 15), clock).Should().BeEmpty();
        WindowDetector.Detect(member, busy, Policy(minWindowNights: 15), clock).Should().BeEmpty();
    }

    [Fact]
    public void Lowering_min_lead_days_opens_an_empty_calendar_that_default_policy_rejects()
    {
        var member = MemberId.New();
        var clock = new MutableClock(AsOf);

        WindowDetector.Detect(member, [], Policy(), clock).Should().BeEmpty();
        var open = WindowDetector.Detect(member, [], Policy(minLeadDays: 0), clock).Should().ContainSingle().Subject;
        open.Start.Should().Be(new DateOnly(2026, 3, 15));
        open.Nights.Should().Be(WindowDetector.LookAheadDays);
    }

    [Fact]
    public void Score_is_the_sum_of_five_named_signals_and_follows_configured_weights()
    {
        var equal = Score(Policy(), watchedBaseline: null);
        equal.Select(signal => signal.Kind).Should().Equal(Enum.GetValues<SignalKind>());
        OpportunityScorer.Total(equal).Should().Be(0.68m);
        OpportunityScorer.Total(equal).Should().Be(equal.Sum(signal => signal.Contribution));

        var windowOnly = Score(Policy(weights: new SignalWeights(1m, 0m, 0m, 0m, 0m)), watchedBaseline: null);
        OpportunityScorer.Total(windowOnly).Should().Be(1m);
        Signal(windowOnly, SignalKind.WindowFit).Contribution.Should().Be(1m);
        Signal(windowOnly, SignalKind.PriceDrop).Contribution.Should().Be(0m);
    }

    [Fact]
    public void A_drop_below_the_configured_threshold_does_not_score_and_raising_it_does()
    {
        var fivePercentLive = Money.Of(95m, Currency.Usd);
        var baseline = Money.Of(100m, Currency.Usd);

        var below = Score(Policy(priceDropPercent: 10m), watchedBaseline: baseline, liveNet: fivePercentLive);
        Signal(below, SignalKind.PriceDrop).RawValue.Should().Be(0.05m);
        Signal(below, SignalKind.PriceDrop).Normalized.Should().Be(0m);

        var crossing = Score(Policy(priceDropPercent: 4m), watchedBaseline: baseline, liveNet: fivePercentLive);
        Signal(crossing, SignalKind.PriceDrop).Normalized.Should().Be(0.05m / OpportunityScorer.PriceDropSaturation);
        crossing.Sum(signal => signal.Contribution).Should().BeGreaterThan(below.Sum(signal => signal.Contribution));
    }

    [Fact]
    public void Raising_the_weekly_cap_allows_a_send_that_the_default_cap_blocks()
    {
        var member = MemberId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var prior = new[]
        {
            Deliver(member, OfferId.New(), window),
            Deliver(member, OfferId.New(), window),
        };
        var clock = new MutableClock(AsOf);
        var nextOffer = OfferId.New();

        FatigueRules.FirstMatch(nextOffer, window, prior, Policy(maxNudges: 2), clock)
            .Should().Be(SuppressionReason.FatigueCapReached);
        FatigueRules.FirstMatch(nextOffer, window, prior, Policy(maxNudges: 3), clock)
            .Should().BeNull();
    }

    [Fact]
    public void Cooldown_days_are_configuration_not_a_hard_coded_window()
    {
        var member = MemberId.New();
        var offer = OfferId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var dismissed = Deliver(member, OfferId.New(), window, at: AsOf.AddDays(-31));
        dismissed.Dismiss();
        var clock = new MutableClock(AsOf);

        FatigueRules.FirstMatch(offer, window, [dismissed], Policy(cooldownDays: 30), clock)
            .Should().BeNull();
        FatigueRules.FirstMatch(offer, window, [dismissed], Policy(cooldownDays: 60), clock)
            .Should().Be(SuppressionReason.CooldownActive);
    }

    [Fact]
    public void Nudge_lifetime_is_taken_from_policy_and_expiry_is_inclusive()
    {
        var member = MemberId.New();
        var window = new TravelWindow(member, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var week = Nudge.Deliver(
            PartnerId.New(),
            member,
            OfferId.New(),
            window,
            [OpportunitySignal.Of(SignalKind.WindowFit, 14m, 1m, 1m)],
            Policy(lifetimeDays: 7),
            new MutableClock(AsOf));
        var day = Nudge.Deliver(
            PartnerId.New(),
            member,
            OfferId.New(),
            window,
            [OpportunitySignal.Of(SignalKind.WindowFit, 14m, 1m, 1m)],
            Policy(lifetimeDays: 1),
            new MutableClock(AsOf));

        week.ExpiresAt.Should().Be(AsOf.AddDays(7));
        day.ExpiresAt.Should().Be(AsOf.AddDays(1));
        week.IsExpired(new MutableClock(AsOf.AddDays(6))).Should().BeFalse();
        week.IsExpired(new MutableClock(AsOf.AddDays(7))).Should().BeTrue();
        day.IsExpired(new MutableClock(AsOf.AddHours(12))).Should().BeFalse();
        day.IsExpired(new MutableClock(AsOf.AddDays(1))).Should().BeTrue();
    }

    private static OpportunityPolicy Policy(
        int minWindowNights = 3,
        int minLeadDays = 14,
        decimal priceDropPercent = 10m,
        int maxNudges = 2,
        int cooldownDays = 30,
        int lifetimeDays = 7,
        SignalWeights? weights = null) =>
        new(
            minWindowNights,
            minLeadDays,
            scoreThreshold: 0.55m,
            Percent.From(priceDropPercent),
            maxNudges,
            cooldownDays,
            lifetimeDays,
            weights ?? Fixtures.Weights);

    private static BusyPeriod[] MayaBusy(PartnerId partner, MemberId member) =>
    [
        BusyPeriod.Create(partner, member, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 29)),
        BusyPeriod.Create(partner, member, new DateOnly(2026, 4, 12), new DateOnly(2026, 5, 1)),
    ];

    private static IReadOnlyList<OpportunitySignal> Score(
        OpportunityPolicy policy,
        Money? watchedBaseline,
        Money? liveNet = null)
    {
        var offer = liveNet is { } net
            ? TravelOffer.Create(
                SupplierId.New(),
                "Coral Bay Resort",
                new Destination("MBJ", "Montego Bay"),
                net,
                Money.Of(15m, Currency.Usd),
                [OfferTag.Beach, OfferTag.Family],
                4,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31))
            : Fixtures.Offer(SupplierId.New());
        var window = new TravelWindow(MemberId.New(), new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var history = Enumerable
            .Range(0, 3)
            .Select(_ => new CompletedStay(offer.Destination, offer.Tags))
            .ToArray();

        return OpportunityScorer.Score(
            window,
            offer,
            policy,
            history,
            Money.Of(120.75m, Currency.Usd),
            Money.Of(48.30m, Currency.Usd),
            Money.Of(60m, Currency.Usd),
            watchedBaseline);
    }

    private static OpportunitySignal Signal(IReadOnlyList<OpportunitySignal> signals, SignalKind kind) =>
        signals.Should().ContainSingle(signal => signal.Kind == kind).Subject;

    private static Nudge Deliver(
        MemberId member,
        OfferId offer,
        TravelWindow window,
        DateTimeOffset? at = null) =>
        Nudge.Deliver(
            PartnerId.New(),
            member,
            offer,
            window,
            [OpportunitySignal.Of(SignalKind.WindowFit, 14m, 1m, 1m)],
            Policy(),
            new MutableClock(at ?? AsOf));
}
