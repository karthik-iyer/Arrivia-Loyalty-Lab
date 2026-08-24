using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Pricing;

internal static class PricingExamples
{
    public static DateTimeOffset AsOf { get; } = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    public static DateOnly Stay { get; } = new(2026, 3, 15);

    public static PricingPipeline Pipeline { get; } = new();

    public static PricingRequest Request(
        PartnerId partner,
        TravelOffer offer,
        TierCode? tier,
        IReadOnlyList<PricingRule> rules) =>
        new(
            PricingContext.ForOffer(partner, offer, tier, Stay),
            offer,
            new HashSet<SupplierId> { offer.SupplierId },
            rules,
            AsOf);

    public static TravelOffer OceanicBeachOffer() =>
        TravelOffer.Create(
            SupplierId.New(),
            "Coral Bay Resort",
            new Destination("MBJ", "Montego Bay"),
            Money.Of(100.00m, Currency.Usd),
            Money.Of(15.00m, Currency.Usd),
            [OfferTag.Beach],
            starRating: 4,
            availableFrom: new DateOnly(2026, 1, 1),
            availableTo: new DateOnly(2026, 6, 30));

    public static List<PricingRule> SummitRules(PartnerId partner) =>
    [
        BaseMarkupRule.Create(partner, Percent.From(12m), RuleScope.PartnerWide, AsOf),
        TierAdjustmentRule.Create(partner, Percent.From(-3m), new RuleScope(tier: TierCode.Gold), AsOf),
        CampaignDiscountRule.Create(
            partner,
            "MARCH-BEACH",
            Percent.From(-5m),
            new RuleScope(tag: OfferTag.Beach),
            AsOf),
        MarginFloorRule.Create(partner, Percent.From(5m), RuleScope.PartnerWide, AsOf),
        BurnCapRule.Create(partner, Percent.From(40m), RuleScope.PartnerWide, AsOf),
    ];

    public static List<PricingRule> NimbusRules(PartnerId partner) =>
    [
        BaseMarkupRule.Create(partner, Percent.From(18m), RuleScope.PartnerWide, AsOf),
        MarginFloorRule.Create(partner, Percent.From(5m), RuleScope.PartnerWide, AsOf),
        BurnCapRule.Create(partner, Percent.From(100m), RuleScope.PartnerWide, AsOf),
    ];
}
