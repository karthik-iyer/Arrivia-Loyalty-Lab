using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;

namespace LoyaltyLab.Application.Booking;

public sealed record StartBookingSagaCommand(
    QuoteId QuoteId,
    int Credits,
    DateOnly StayDate,
    string IdempotencyKey,
    string CorrelationId);

public sealed record GetBookingQuery(BookingId BookingId);

public sealed record CancelBookingCommand(BookingId BookingId, string IdempotencyKey);

public sealed record GetSagaInstanceQuery(SagaInstanceId SagaId);

public sealed record ListSagasQuery;

public sealed record RunAdminWorkerCommand(string Worker);

public sealed record BookingResult(
    BookingId BookingId,
    BookingStatus Status,
    TenderSplit Tender,
    RateDriftOutcome? Drift,
    SagaSummary Saga)
{
    public static BookingResult From(Domain.Booking.Booking booking, SagaInstance saga) =>
        new(booking.Id, booking.Status, booking.Tender, booking.Drift, SagaSummary.From(saga));
}

public sealed record SagaSummary(
    SagaInstanceId Id,
    SagaStatus Status,
    IReadOnlyList<SagaStepSummary> Steps)
{
    public static SagaSummary From(SagaInstance saga) =>
        new(saga.Id, saga.Status, saga.Steps.Select(SagaStepSummary.From).ToArray());
}

public sealed record SagaStepSummary(
    SagaStepKind Kind,
    SagaStepStatus Status,
    int Attempts,
    string? ExternalReference,
    Error? LastError,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    CompensationRecord? Compensation)
{
    public static SagaStepSummary From(SagaStepRecord step) =>
        new(
            step.Kind,
            step.Status,
            step.Attempts,
            step.ExternalReference,
            step.LastError,
            step.StartedAt,
            step.CompletedAt,
            step.Compensation);

    public int? DurationMs =>
        StartedAt is { } start && CompletedAt is { } end
            ? (int)Math.Max(0, (end - start).TotalMilliseconds)
            : null;
}

public sealed record SagaListItem(
    SagaInstanceId Id,
    BookingId BookingId,
    SagaStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset LastHeartbeatAt);

public sealed record PoisonHttpItem(
    Guid Id,
    string Type,
    string CorrelationId,
    int Attempts,
    string LastError,
    DateTimeOffset PoisonedAt);

public sealed record SagaOperatorDetail(
    SagaSummary Saga,
    BookingId BookingId,
    DateTimeOffset StartedAt,
    DateTimeOffset LastHeartbeatAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<PoisonHttpItem> Poison);

public sealed record RunAdminWorkerResult(string Worker, int Processed);
