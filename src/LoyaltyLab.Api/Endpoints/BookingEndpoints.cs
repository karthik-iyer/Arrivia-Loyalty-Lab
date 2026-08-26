using LoyaltyLab.Api.Http;
using LoyaltyLab.Application.Booking;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;

namespace LoyaltyLab.Api.Endpoints;

internal static class BookingEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static void MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/bookings", StartAsync)
            .WithTags("Booking")
            .WithSummary("Start checkout as a saga. Requires Idempotency-Key.")
            .Produces<BookingHttp>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        app.MapGet("/api/bookings/{bookingId:guid}", GetAsync)
            .WithTags("Booking")
            .WithSummary("Get a booking and its saga.")
            .Produces<BookingHttp>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost("/api/bookings/{bookingId:guid}/cancel", CancelAsync)
            .WithTags("Booking")
            .WithSummary("Cancel a booking. Requires Idempotency-Key.")
            .Produces<BookingHttp>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        app.MapGet("/api/operator/sagas", ListSagasAsync)
            .WithTags("Operator")
            .WithSummary("List sagas for the resolved partner. Operator.")
            .Produces<SagaListHttp[]>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        app.MapGet("/api/operator/sagas/{sagaId:guid}", GetSagaAsync)
            .WithTags("Operator")
            .WithSummary("Saga detail including compensation. Operator.")
            .Produces<SagaOperatorHttp>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost("/api/admin/run/{worker}", RunWorkerAsync)
            .WithTags("Operator")
            .WithSummary("Run an admin worker (scan, expire, …). Operator.")
            .Produces<AdminWorkerHttp>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> StartAsync(
        StartBookingHttp? body,
        StartBookingSaga start,
        HttpContext http,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (body is null)
        {
            return ProblemResults.FromError(http, Errors.QuoteNotFound);
        }

        var stay = body.StayDate ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var result = await start.ExecuteAsync(
            new StartBookingSagaCommand(
                new QuoteId(body.QuoteId),
                body.Credits,
                stay,
                Key(http) ?? string.Empty,
                http.TraceIdentifier),
            cancellationToken);
        return result.Match(
            booking => Results.Ok(BookingHttp.From(booking)),
            error => ProblemResults.FromError(http, error));
    }

    private static async Task<IResult> GetAsync(
        Guid bookingId,
        GetBooking getBooking,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await getBooking.ExecuteAsync(new GetBookingQuery(new BookingId(bookingId)), cancellationToken);
        return result.Match(
            booking => Results.Ok(BookingHttp.From(booking)),
            error => ProblemResults.FromError(http, error));
    }

    private static async Task<IResult> CancelAsync(
        Guid bookingId,
        CancelBooking cancel,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await cancel.ExecuteAsync(
            new CancelBookingCommand(new BookingId(bookingId), Key(http) ?? string.Empty),
            cancellationToken);
        return result.Match(
            booking => Results.Ok(BookingHttp.From(booking)),
            error => ProblemResults.FromError(http, error));
    }

    private static async Task<IResult> ListSagasAsync(
        ListSagas list,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await list.ExecuteAsync(new ListSagasQuery(), cancellationToken);
        return result.Match(
            sagas => Results.Ok(sagas.Select(SagaListHttp.From).ToArray()),
            error => ProblemResults.FromError(http, error));
    }

    private static async Task<IResult> GetSagaAsync(
        Guid sagaId,
        GetSagaInstance getSaga,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await getSaga.ExecuteAsync(new GetSagaInstanceQuery(new SagaInstanceId(sagaId)), cancellationToken);
        return result.Match(
            saga => Results.Ok(SagaOperatorHttp.From(saga)),
            error => ProblemResults.FromError(http, error));
    }

    private static async Task<IResult> RunWorkerAsync(
        string worker,
        RunAdminWorker run,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await run.ExecuteAsync(new RunAdminWorkerCommand(worker), cancellationToken);
        return result.Match(
            outcome => Results.Ok(new AdminWorkerHttp(outcome.Worker, outcome.Processed)),
            error => ProblemResults.FromError(http, error));
    }

    private static string? Key(HttpContext http) =>
        http.Request.Headers[IdempotencyHeader].FirstOrDefault();
}

internal sealed record StartBookingHttp(Guid QuoteId, int Credits, DateOnly? StayDate);

internal sealed record TenderHttp(MoneyHttp Cash, int Credits)
{
    public static TenderHttp From(TenderSplit tender) =>
        new(MoneyHttp.From(tender.CashAmount), tender.CreditsApplied);
}

internal sealed record DriftHttp(string Applied, MoneyHttp? NetRateDelta)
{
    public static DriftHttp? From(RateDriftOutcome? drift) =>
        drift is null
            ? null
            : new DriftHttp(drift.Kind.ToString(), MoneyHttp.From(drift.NetRateDelta));
}

internal sealed record SagaStepHttp(
    SagaStepKind Kind,
    SagaStepStatus Status,
    int Attempts,
    string? ExternalReference,
    string? ErrorCode,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int? DurationMs,
    CompensationHttp? Compensation)
{
    public static SagaStepHttp From(SagaStepSummary step) =>
        new(
            step.Kind,
            step.Status,
            step.Attempts,
            step.ExternalReference,
            step.LastError?.Code,
            step.StartedAt,
            step.CompletedAt,
            step.DurationMs,
            CompensationHttp.From(step.Compensation));
}

internal sealed record CompensationHttp(
    CompensationStatus Status,
    int Attempts,
    string? ExternalReference,
    string? ErrorCode)
{
    public static CompensationHttp? From(CompensationRecord? record) =>
        record is null
            ? null
            : new CompensationHttp(record.Status, record.Attempts, record.ExternalReference, record.LastError?.Code);
}

internal sealed record SagaHttp(Guid Id, SagaStatus Status, IReadOnlyList<SagaStepHttp> Steps)
{
    public static SagaHttp From(SagaSummary saga) =>
        new(saga.Id.Value, saga.Status, saga.Steps.Select(SagaStepHttp.From).ToArray());
}

internal sealed record BookingHttp(
    Guid BookingId,
    BookingStatus Status,
    TenderHttp Tender,
    DriftHttp? Drift,
    SagaHttp Saga)
{
    public static BookingHttp From(BookingResult booking) =>
        new(
            booking.BookingId.Value,
            booking.Status,
            TenderHttp.From(booking.Tender),
            DriftHttp.From(booking.Drift),
            SagaHttp.From(booking.Saga));
}

internal sealed record SagaListHttp(
    Guid Id,
    Guid BookingId,
    SagaStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset LastHeartbeatAt)
{
    public static SagaListHttp From(SagaListItem item) =>
        new(item.Id.Value, item.BookingId.Value, item.Status, item.StartedAt, item.LastHeartbeatAt);
}

internal sealed record PoisonOperatorHttp(
    Guid Id,
    string Type,
    string CorrelationId,
    int Attempts,
    string LastError,
    DateTimeOffset PoisonedAt)
{
    public static PoisonOperatorHttp From(PoisonHttpItem item) =>
        new(item.Id, item.Type, item.CorrelationId, item.Attempts, item.LastError, item.PoisonedAt);
}

internal sealed record SagaOperatorHttp(
    Guid Id,
    Guid BookingId,
    SagaStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset LastHeartbeatAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<SagaStepHttp> Steps,
    IReadOnlyList<PoisonOperatorHttp> Poison)
{
    public static SagaOperatorHttp From(SagaOperatorDetail detail) =>
        new(
            detail.Saga.Id.Value,
            detail.BookingId.Value,
            detail.Saga.Status,
            detail.StartedAt,
            detail.LastHeartbeatAt,
            detail.CompletedAt,
            detail.Saga.Steps.Select(SagaStepHttp.From).ToArray(),
            detail.Poison.Select(PoisonOperatorHttp.From).ToArray());
}

internal sealed record AdminWorkerHttp(string Worker, int Processed);
