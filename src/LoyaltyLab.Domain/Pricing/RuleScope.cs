using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Pricing;

/// <summary>
/// Optional filters that narrow a rule. Specificity is the count of populated dimensions (FR-P-04).
/// </summary>
public sealed record RuleScope
{
    public RuleScope(
        TierCode? tier = null,
        SupplierId? supplierId = null,
        OfferTag? tag = null,
        string? destinationCode = null,
        OfferId? offerId = null)
    {
        Tier = tier;
        SupplierId = supplierId;
        Tag = tag;
        DestinationCode = string.IsNullOrWhiteSpace(destinationCode)
            ? null
            : destinationCode.Trim().ToUpperInvariant();
        OfferId = offerId;
    }

    public static RuleScope PartnerWide { get; } = new();

    public TierCode? Tier { get; }

    public SupplierId? SupplierId { get; }

    public OfferTag? Tag { get; }

    public string? DestinationCode { get; }

    public OfferId? OfferId { get; }

    public int Specificity =>
        (Tier is null ? 0 : 1)
        + (SupplierId is null ? 0 : 1)
        + (Tag is null ? 0 : 1)
        + (DestinationCode is null ? 0 : 1)
        + (OfferId is null ? 0 : 1);

    public bool Matches(PricingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Tier is { } tier && context.Tier != tier)
        {
            return false;
        }

        if (SupplierId is { } supplier && context.SupplierId != supplier)
        {
            return false;
        }

        if (Tag is { } tag && !context.Tags.Contains(tag))
        {
            return false;
        }

        if (DestinationCode is { } destination && context.DestinationCode != destination)
        {
            return false;
        }

        if (OfferId is { } offer && context.OfferId != offer)
        {
            return false;
        }

        return true;
    }
}
