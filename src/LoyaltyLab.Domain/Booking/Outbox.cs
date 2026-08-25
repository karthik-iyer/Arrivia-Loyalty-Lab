using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Booking;

/// <summary>
/// Stable outbox type names. Downstream handlers match on these strings (ADR-0007).
/// </summary>
public static class OutboxMessageTypes
{
    public const string BookingConfirmed = "booking.confirmed";

    public const string CreditsBurned = "credits.burned";

    public const string BookingCompensated = "booking.compensated";

    public const string BookingRequiresManualReview = "booking.requires-manual-review";

    public static IReadOnlyList<string> All { get; } =
    [
        BookingConfirmed,
        CreditsBurned,
        BookingCompensated,
        BookingRequiresManualReview,
    ];
}

/// <summary>
/// Written in the same transaction as the state change it describes (FR-B-06).
/// The dispatcher delivers at least once, then marks <see cref="DispatchedAt"/>.
/// </summary>
public sealed class OutboxMessage : ITenantOwned
{
    private OutboxMessage()
    {
        Type = null!;
        Payload = null!;
        CorrelationId = null!;
    }

    private OutboxMessage(
        Guid id,
        PartnerId partnerId,
        string type,
        string payload,
        string correlationId,
        DateTimeOffset occurredAt)
    {
        Id = id;
        PartnerId = partnerId;
        Type = type;
        Payload = payload;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
        Attempts = 0;
    }

    public Guid Id { get; private set; }

    public PartnerId PartnerId { get; private set; }

    public string Type { get; private set; }

    public string Payload { get; private set; }

    public string CorrelationId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? DispatchedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public bool IsDispatched => DispatchedAt is not null;

    public static OutboxMessage Create(
        PartnerId partnerId,
        string type,
        string payload,
        string correlationId,
        IClock clock,
        Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new DomainException("An outbox message requires a type.");
        }

        if (payload is null)
        {
            throw new DomainException("An outbox message requires a payload.");
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new DomainException("An outbox message requires a correlation id.");
        }

        return new OutboxMessage(
            id ?? EntityIds.NewValue(),
            partnerId,
            type.Trim(),
            payload,
            correlationId.Trim(),
            clock.UtcNow);
    }

    public void RecordAttempt(string error)
    {
        if (IsDispatched)
        {
            throw new DomainException("A dispatched outbox message cannot record another attempt.");
        }

        if (string.IsNullOrWhiteSpace(error))
        {
            throw new DomainException("A failed attempt requires an error.");
        }

        Attempts++;
        LastError = error.Trim();
    }

    public void MarkDispatched(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (IsDispatched)
        {
            return;
        }

        DispatchedAt = clock.UtcNow;
    }
}

/// <summary>
/// An outbox message that exhausted retries. Moved here so it cannot block dispatch (FR-B-07).
/// </summary>
public sealed class PoisonMessage : ITenantOwned
{
    private PoisonMessage()
    {
        Type = null!;
        Payload = null!;
        CorrelationId = null!;
        LastError = null!;
    }

    private PoisonMessage(
        Guid id,
        Guid outboxMessageId,
        PartnerId partnerId,
        string type,
        string payload,
        string correlationId,
        DateTimeOffset occurredAt,
        DateTimeOffset poisonedAt,
        int attempts,
        string lastError)
    {
        Id = id;
        OutboxMessageId = outboxMessageId;
        PartnerId = partnerId;
        Type = type;
        Payload = payload;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
        PoisonedAt = poisonedAt;
        Attempts = attempts;
        LastError = lastError;
    }

    public Guid Id { get; private set; }

    public Guid OutboxMessageId { get; private set; }

    public PartnerId PartnerId { get; private set; }

    public string Type { get; private set; }

    public string Payload { get; private set; }

    public string CorrelationId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset PoisonedAt { get; private set; }

    public int Attempts { get; private set; }

    public string LastError { get; private set; }

    public static PoisonMessage From(OutboxMessage message, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(clock);
        if (string.IsNullOrWhiteSpace(message.LastError))
        {
            throw new DomainException("A poison message requires the last delivery error.");
        }

        return new PoisonMessage(
            EntityIds.NewValue(),
            message.Id,
            message.PartnerId,
            message.Type,
            message.Payload,
            message.CorrelationId,
            message.OccurredAt,
            clock.UtcNow,
            message.Attempts,
            message.LastError);
    }
}
