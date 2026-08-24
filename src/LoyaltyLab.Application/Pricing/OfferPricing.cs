using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Pricing;

internal static class OfferPricing
{
    private static readonly PricingPipeline Pipeline = new();

    public static PricingState Run(
        PartnerId partnerId,
        TravelOffer offer,
        TierCode? tier,
        DateOnly stayDate,
        IReadOnlySet<SupplierId> permitted,
        IReadOnlyList<PricingRule> rules,
        DateTimeOffset asOf) =>
        Pipeline.Execute(
            new PricingRequest(
                PricingContext.ForOffer(partnerId, offer, tier, stayDate),
                offer,
                permitted,
                rules,
                asOf));
}
