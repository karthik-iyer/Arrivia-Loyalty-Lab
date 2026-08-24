namespace LoyaltyLab.PaymentSim;

internal static class PaymentEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/payments/authorizations", AuthorizeAsync);
        app.MapPost("/payments/authorizations/{id:guid}/capture", CaptureAsync);
        app.MapPost("/payments/authorizations/{id:guid}/void", VoidAsync);
        app.MapPost("/payments/authorizations/{id:guid}/refund", RefundAsync);
        app.MapGet("/payments/by-key", ByKeyAsync);
        app.MapGet("/payments", ListAsync);
    }

    private static async Task<IResult> AuthorizeAsync(
        AuthorizeHttp body,
        PaymentProcessor payments,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        await DelayAsync(payments.LatencyMs, cancellationToken);
        var result = payments.Authorize(new PaymentCommand(body.Amount, body.Currency, body.Description), Key(http));
        if (result.Error == PaymentError.None && payments.ShouldHang(result.IsReplay))
        {
            await DelayAsync(payments.TimeoutHangMs, cancellationToken);
        }

        return ToHttp(http, result, created: !result.IsReplay);
    }

    private static Task<IResult> CaptureAsync(
        Guid id,
        PaymentProcessor payments,
        HttpContext http) =>
        Task.FromResult(ToHttp(http, payments.Capture(id, Key(http))));

    private static Task<IResult> VoidAsync(
        Guid id,
        PaymentProcessor payments,
        HttpContext http) =>
        Task.FromResult(ToHttp(http, payments.Void(id, Key(http))));

    private static Task<IResult> RefundAsync(
        Guid id,
        PaymentProcessor payments,
        HttpContext http) =>
        Task.FromResult(ToHttp(http, payments.Refund(id, Key(http))));

    private static IResult ByKeyAsync(string? key, PaymentProcessor payments, HttpContext http) =>
        ToHttp(http, payments.FindByKey(key), asQuery: true);

    private static IResult ListAsync(PaymentProcessor payments) =>
        Results.Ok(payments.List().Select(intent => PaymentHttp.From(intent)).ToArray());

    private static string? Key(HttpContext http) =>
        http.Request.Headers[IdempotencyHeader].FirstOrDefault();

    private static async Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        if (milliseconds > 0)
        {
            await Task.Delay(milliseconds, cancellationToken);
        }
    }

    private static IResult ToHttp(HttpContext http, PaymentResult result, bool created = false, bool asQuery = false)
    {
        if (result.Error != PaymentError.None)
        {
            return Problem(http, result.Error);
        }

        var payload = PaymentHttp.From(result.Intent!, result.IsReplay);
        if (!asQuery && result.Intent!.Status == PaymentStatus.Declined)
        {
            return Results.Json(payload, statusCode: StatusCodes.Status402PaymentRequired);
        }

        return created && !result.IsReplay
            ? Results.Json(payload, statusCode: StatusCodes.Status201Created)
            : Results.Ok(payload);
    }

    private static IResult Problem(HttpContext http, PaymentError error)
    {
        var (status, code, title) = error switch
        {
            PaymentError.MissingIdempotencyKey => (StatusCodes.Status400BadRequest, "MISSING_IDEMPOTENCY_KEY", "Every payment mutation requires an Idempotency-Key."),
            PaymentError.InvalidAmount => (StatusCodes.Status400BadRequest, "INVALID_AMOUNT", "Amount must be positive and currency is required."),
            PaymentError.NotFound => (StatusCodes.Status404NotFound, "PAYMENT_NOT_FOUND", "The payment was not found."),
            PaymentError.IdempotencyKeyReused => (StatusCodes.Status409Conflict, "IDEMPOTENCY_KEY_REUSED", "This idempotency key was already used with a different payload or operation."),
            PaymentError.InvalidState => (StatusCodes.Status409Conflict, "INVALID_STATE", "This payment cannot move to that status."),
            _ => (StatusCodes.Status400BadRequest, "PAYMENT_ERROR", "The payment request was rejected."),
        };

        return Results.Problem(
            title: title,
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = code,
                ["correlationId"] = http.TraceIdentifier,
            });
    }
}

internal sealed record AuthorizeHttp(decimal Amount, string Currency, string? Description);

internal sealed record PaymentHttp(
    Guid Id,
    PaymentStatus Status,
    decimal Amount,
    string Currency,
    string AuthorizeKey,
    DateTimeOffset CreatedAt,
    bool IsReplay)
{
    public static PaymentHttp From(PaymentIntent intent, bool isReplay = false) =>
        new(
            intent.Id,
            intent.Status,
            intent.Amount,
            intent.Currency,
            intent.AuthorizeKey,
            intent.CreatedAt,
            isReplay);
}
