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

    public static Error OfferNotEligible { get; } =
        Error.Of("OFFER_NOT_ELIGIBLE", "The offer is excluded by partner or tier rules.");

    public static Error QuoteNotFound { get; } =
        Error.Of("QUOTE_NOT_FOUND", "The quote was not found.");

    public static Error QuoteExpired { get; } =
        Error.Of("QUOTE_EXPIRED", "The quote has expired; re-quote required.");

    public static Error RateChanged { get; } =
        Error.Of("RATE_CHANGED", "The supplier rate moved beyond tolerance.");

    public static Error LedgerUnbalanced { get; } =
        Error.Of("LEDGER_UNBALANCED", "A ledger transaction must consist of legs that sum to zero.");
}
