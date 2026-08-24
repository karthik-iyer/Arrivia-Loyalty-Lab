using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Pricing;

/// <summary>
/// The facts a rule needs to decide whether it applies. Built once per pricing run (FR-P-01).
/// </summary>
public sealed record PricingContext
{
    public PricingContext(
        PartnerId partnerId,
        SupplierId supplierId,
        OfferId offerId,
        string destinationCode,
        IEnumerable<OfferTag> tags,
        TierCode? tier,
        DateOnly stayDate)
    {
        if (string.IsNullOrWhiteSpace(destinationCode))
        {
            throw new DomainException("Destination code is required.");
        }

        PartnerId = partnerId;
        SupplierId = supplierId;
        OfferId = offerId;
        DestinationCode = destinationCode.Trim().ToUpperInvariant();
        Tags = tags.ToHashSet();
        Tier = tier;
        StayDate = stayDate;
    }

    public PartnerId PartnerId { get; }

    public SupplierId SupplierId { get; }

    public OfferId OfferId { get; }

    public string DestinationCode { get; }

    public IReadOnlySet<OfferTag> Tags { get; }

    public TierCode? Tier { get; }

    public DateOnly StayDate { get; }

    public static PricingContext ForOffer(PartnerId partnerId, TravelOffer offer, TierCode? tier, DateOnly stayDate) =>
        new(partnerId, offer.SupplierId, offer.Id, offer.Destination.Code, offer.Tags, tier, stayDate);
}
