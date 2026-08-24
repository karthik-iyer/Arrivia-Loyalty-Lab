using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests;

internal static class Fixtures
{
    public static PartnerTheme Theme { get; } = new("#BE185D", "#FFF7ED", "#1D4ED8");

    public static CreditPolicy Credits { get; } =
        new(0.01m, Percent.From(40m), 730, Percent.From(10m));

    public static QuotePolicy Quotes { get; } =
        new(15, RateDriftPolicy.AbsorbWithinTolerance, Percent.From(2m));

    public static SagaPolicy Sagas { get; } =
        new(10, 3, 5, 60);

    public static SignalWeights Weights { get; } =
        new(0.2m, 0.2m, 0.2m, 0.2m, 0.2m);

    public static OpportunityPolicy Opportunities { get; } = new(
        minWindowNights: 3,
        minLeadDays: 14,
        scoreThreshold: 0.55m,
        priceDropThreshold: Percent.From(10m),
        maxNudgesPerMemberPerWeek: 2,
        dismissalCooldownDays: 30,
        nudgeLifetimeDays: 7,
        weights: Weights);

    public static Partner Summit() =>
        Partner.Create(
            "summit",
            "Summit Rewards",
            Currency.Usd,
            Theme,
            Credits,
            Quotes,
            Sagas,
            Opportunities);

    public static TravelOffer Offer(SupplierId supplierId) =>
        TravelOffer.Create(
            supplierId,
            "Coral Bay Resort",
            new Destination("MBJ", "Montego Bay"),
            Money.Of(100.00m, Currency.Usd),
            Money.Of(15.00m, Currency.Usd),
            [OfferTag.Beach, OfferTag.Family],
            starRating: 4,
            availableFrom: new DateOnly(2026, 1, 1),
            availableTo: new DateOnly(2026, 12, 31));
}
