using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;

namespace LoyaltyLab.Domain.Booking;

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2,
    Failed = 3,
}

public sealed record TenderSplit
{
    public TenderSplit(Money cashAmount, int creditsApplied, Money creditValue)
    {
        if (creditsApplied < 0)
        {
            throw new DomainException("Credits applied cannot be negative.");
        }

        if (cashAmount.IsNegative || creditValue.IsNegative)
        {
            throw new DomainException("Tender amounts cannot be negative.");
        }

        if (cashAmount.Currency != creditValue.Currency)
        {
            throw new DomainException("Cash and credit legs must use the same currency.");
        }

        CashAmount = cashAmount;
        CreditsApplied = creditsApplied;
        CreditValue = creditValue;
    }

    public Money CashAmount { get; }

    public int CreditsApplied { get; }

    public Money CreditValue { get; }

    public bool HasCash => !CashAmount.IsZero;

    public bool HasCredits => CreditsApplied > 0;
}

/// <summary>
/// Checkout record the saga confirms or cancels. Persistence of the table is T-039;
/// the aggregate is required so ConfirmBooking has something to compensate.
/// </summary>
public sealed class Booking : Entity<BookingId>
{
    private Booking()
    {
        Tender = null!;
    }

    private Booking(
        BookingId id,
        PartnerId partnerId,
        MemberId memberId,
        QuoteId quoteId,
        TenderSplit tender)
        : base(id)
    {
        PartnerId = partnerId;
        MemberId = memberId;
        QuoteId = quoteId;
        Tender = tender;
        Status = BookingStatus.Pending;
    }

    public PartnerId PartnerId { get; private set; }

    public MemberId MemberId { get; private set; }

    public QuoteId QuoteId { get; private set; }

    public TenderSplit Tender { get; private set; }

    public BookingStatus Status { get; private set; }

    public RateDriftOutcome? Drift { get; private set; }

    public string? SupplierReference { get; private set; }

    public static Booking Place(
        BookingId id,
        PartnerId partnerId,
        MemberId memberId,
        QuoteId quoteId,
        TenderSplit tender)
    {
        ArgumentNullException.ThrowIfNull(tender);
        return new Booking(id, partnerId, memberId, quoteId, tender);
    }

    public void Confirm(string? supplierReference, RateDriftOutcome? drift)
    {
        if (Status is BookingStatus.Cancelled or BookingStatus.Failed)
        {
            throw new DomainException($"A {Status} booking cannot be confirmed.");
        }

        Status = BookingStatus.Confirmed;
        if (supplierReference is not null)
        {
            SupplierReference = supplierReference;
        }

        if (drift is not null)
        {
            Drift = drift;
        }
    }

    public void Cancel()
    {
        if (Status == BookingStatus.Cancelled)
        {
            return;
        }

        Status = BookingStatus.Cancelled;
    }
}
