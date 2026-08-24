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

    public static Error IdempotencyKeyReused { get; } =
        Error.Of("IDEMPOTENCY_KEY_REUSED", "This idempotency key was already used with a different payload.");

    public static Error BurnCapExceeded { get; } =
        Error.Of("BURN_CAP_EXCEEDED", "The credit tender exceeds the partner burn cap for this booking.");

    public static Error InsufficientCredits { get; } =
        Error.Of("INSUFFICIENT_CREDITS", "The credit tender exceeds the available balance.");

    public static Error MemberNotFound { get; } =
        Error.Of("MEMBER_NOT_FOUND", "The member was not found.");

    public static Error LedgerTransactionNotFound { get; } =
        Error.Of("LEDGER_TRANSACTION_NOT_FOUND", "The ledger transaction was not found.");

    public static Error TransactionAlreadyReversed { get; } =
        Error.Of("TRANSACTION_ALREADY_REVERSED", "This ledger transaction has already been reversed.");

    public static Error RoleNotPermitted { get; } =
        Error.Of("ROLE_NOT_PERMITTED", "This operation requires a different access role.");

    public static Error PaymentDeclined { get; } =
        Error.Of("PAYMENT_DECLINED", "Authorization or capture was refused.");

    public static Error PaymentNotFound { get; } =
        Error.Of("PAYMENT_NOT_FOUND", "The payment was not found.");

    public static Error SupplierUnavailable { get; } =
        Error.Of("SUPPLIER_UNAVAILABLE", "The reservation could not be placed.");
}
