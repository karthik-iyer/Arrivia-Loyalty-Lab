namespace LoyaltyLab.Domain.Common;

/// <summary>
/// Stable business-failure codes from the error catalog (docs/04 §9).
/// HTTP mapping lives at the API boundary, not here.
/// </summary>
public static class Errors
{
    public static Error PartnerNotResolved { get; } =
        Error.Of("PARTNER_NOT_RESOLVED", "Every request must resolve to exactly one partner.");

    public static Error OfferNotFound { get; } =
        Error.Of("OFFER_NOT_FOUND", "The offer was not found.");
}
