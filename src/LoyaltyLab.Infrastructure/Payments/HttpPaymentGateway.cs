using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using Polly.Timeout;

namespace LoyaltyLab.Infrastructure.Payments;

internal static class PaymentSimFaultHeaders
{
    public const string ForceDecline = "X-Sim-Force-Decline";

    public const string ForceTimeout = "X-Sim-Force-Timeout";
}

public sealed class HttpPaymentGateway(HttpClient http, IEnumerable<IFaultInjector> faults) : IPaymentGateway
{
    private readonly IFaultInjector? _faults = faults.FirstOrDefault();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Task<StepOutcome> AuthorizeAsync(PaymentAuthorizeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = new
        {
            amount = request.Amount.Amount,
            currency = request.Amount.Currency.Code,
            description = request.Description,
        };
        return SendAsync(HttpMethod.Post, "/payments/authorizations", request.IdempotencyKey, body, cancellationToken);
    }

    public Task<StepOutcome> CaptureAsync(PaymentReferenceRequest request, CancellationToken cancellationToken) =>
        MutateAsync(request, "capture", cancellationToken);

    public Task<StepOutcome> VoidAsync(PaymentReferenceRequest request, CancellationToken cancellationToken) =>
        MutateAsync(request, "void", cancellationToken);

    public Task<StepOutcome> RefundAsync(PaymentReferenceRequest request, CancellationToken cancellationToken) =>
        MutateAsync(request, "refund", cancellationToken);

    public Task<StepOutcome> QueryByKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var path = $"/payments/by-key?key={Uri.EscapeDataString(idempotencyKey.Trim())}";
        return SendAsync(HttpMethod.Get, path, idempotencyKey: null, body: (object?)null, cancellationToken);
    }

    private Task<StepOutcome> MutateAsync(
        PaymentReferenceRequest request,
        string action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync(
            HttpMethod.Post,
            $"/payments/authorizations/{request.PaymentId}/{action}",
            request.IdempotencyKey,
            body: (object?)null,
            cancellationToken);
    }

    private async Task<StepOutcome> SendAsync(
        HttpMethod method,
        string path,
        string? idempotencyKey,
        object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = _faults?.Current ?? FaultProfile.None;
            var latency = profile.AddedLatencyMs ?? 0;
            if (latency > 0)
            {
                await Task.Delay(latency, cancellationToken);
            }

            using var request = new HttpRequestMessage(method, path);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            }

            ApplyAuthorizeFaults(request, method, path, profile);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: Json);
            }

            using var response = await http.SendAsync(request, cancellationToken);
            return await MapAsync(response, cancellationToken);
        }
        catch (Exception exception) when (IsRemoteTimeout(exception, cancellationToken))
        {
            return StepOutcome.Unknown();
        }
    }

    private static void ApplyAuthorizeFaults(
        HttpRequestMessage request,
        HttpMethod method,
        string path,
        FaultProfile profile)
    {
        if (method != HttpMethod.Post
            || !string.Equals(path, "/payments/authorizations", StringComparison.Ordinal))
        {
            return;
        }

        if (profile.PaymentDecline)
        {
            request.Headers.TryAddWithoutValidation(PaymentSimFaultHeaders.ForceDecline, "true");
        }

        if (profile.PaymentTimeout)
        {
            request.Headers.TryAddWithoutValidation(PaymentSimFaultHeaders.ForceTimeout, "true");
        }
    }

    private static async Task<StepOutcome> MapAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.RequestTimeout
            || response.StatusCode == HttpStatusCode.GatewayTimeout)
        {
            return StepOutcome.Unknown();
        }

        var payload = await ReadBodyAsync(response, cancellationToken);
        var reference = TryId(payload);
        var errorCode = TryErrorCode(payload);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
        {
            return IsDeclined(payload)
                ? StepOutcome.Failed(Errors.PaymentDeclined, reference)
                : StepOutcome.Succeeded(reference);
        }

        if (response.StatusCode == HttpStatusCode.PaymentRequired || IsDeclined(payload))
        {
            return StepOutcome.Failed(Errors.PaymentDeclined, reference);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return StepOutcome.Failed(Errors.PaymentNotFound, reference);
        }

        if (response.StatusCode == HttpStatusCode.Conflict && errorCode == Errors.IdempotencyKeyReused.Code)
        {
            return StepOutcome.Failed(Errors.IdempotencyKeyReused, reference);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return StepOutcome.Failed(Errors.IdempotencyKeyReused, reference);
        }

        return StepOutcome.Failed(Errors.PaymentDeclined, reference);
    }

    private static async Task<JsonElement?> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return null;
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<JsonElement>(stream, Json, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryId(JsonElement? payload)
    {
        if (payload is { } json && json.ValueKind == JsonValueKind.Object && json.TryGetProperty("id", out var id))
        {
            return id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetGuid().ToString();
        }

        return null;
    }

    private static string? TryErrorCode(JsonElement? payload)
    {
        if (payload is { } json && json.ValueKind == JsonValueKind.Object && json.TryGetProperty("errorCode", out var code))
        {
            return code.GetString();
        }

        return null;
    }

    private static bool IsDeclined(JsonElement? payload)
    {
        if (payload is not { } json || json.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (json.TryGetProperty("status", out var status)
            && string.Equals(status.GetString(), "Declined", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return json.TryGetProperty("errorCode", out var code)
            && code.GetString() == Errors.PaymentDeclined.Code;
    }

    private static bool IsRemoteTimeout(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is TimeoutRejectedException
            or TimeoutException
            or OperationCanceledException;
    }
}
