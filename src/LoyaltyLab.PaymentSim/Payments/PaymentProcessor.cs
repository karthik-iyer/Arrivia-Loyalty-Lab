using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace LoyaltyLab.PaymentSim;

internal enum PaymentStatus
{
    Authorized = 0,
    Declined = 1,
    Captured = 2,
    Voided = 3,
    Refunded = 4,
}

internal sealed class PaymentIntent
{
    public required Guid Id { get; init; }

    public required string AuthorizeKey { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string PayloadHash { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public PaymentStatus Status { get; set; }

    public string? CaptureKey { get; set; }

    public string? VoidKey { get; set; }

    public string? RefundKey { get; set; }
}

internal sealed record PaymentCommand(decimal Amount, string Currency, string? Description);

internal enum PaymentError
{
    None = 0,
    MissingIdempotencyKey = 1,
    IdempotencyKeyReused = 2,
    NotFound = 3,
    InvalidState = 4,
    InvalidAmount = 5,
}

internal readonly record struct PaymentResult(PaymentIntent? Intent, PaymentError Error, bool IsReplay)
{
    public bool IsConflict => Error is PaymentError.IdempotencyKeyReused or PaymentError.InvalidState;

    public static PaymentResult Ok(PaymentIntent intent, bool isReplay = false) =>
        new(intent, PaymentError.None, isReplay);

    public static PaymentResult Fail(PaymentError error) => new(null, error, IsReplay: false);
}

/// <summary>
/// In-memory processor. Insert-first on the idempotency key so two concurrent
/// requests with the same key produce one intent (ADR-0006).
/// </summary>
internal sealed class PaymentProcessor(IOptions<SimulatorOptions> options, IClock clock)
{
    private readonly ConcurrentDictionary<string, PaymentIntent> _byKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, PaymentIntent> _byId = new();
    private readonly Lock _gate = new();

    public IReadOnlyList<PaymentIntent> List()
    {
        lock (_gate)
        {
            return [.. _byId.Values.OrderBy(intent => intent.CreatedAt)];
        }
    }

    public PaymentResult Authorize(PaymentCommand command, string? idempotencyKey)
    {
        if (!TryValidate(command, idempotencyKey, out var key, out var error))
        {
            return PaymentResult.Fail(error);
        }

        var hash = Hash(command);
        lock (_gate)
        {
            if (_byKey.TryGetValue(key, out var existing))
            {
                return existing.PayloadHash == hash
                    ? PaymentResult.Ok(existing, isReplay: true)
                    : PaymentResult.Fail(PaymentError.IdempotencyKeyReused);
            }

            var intent = new PaymentIntent
            {
                Id = Guid.CreateVersion7(),
                AuthorizeKey = key,
                Amount = command.Amount,
                Currency = command.Currency.Trim().ToUpperInvariant(),
                PayloadHash = hash,
                CreatedAt = clock.UtcNow,
                Status = CoinFlip(options.Value.DeclineRate) ? PaymentStatus.Declined : PaymentStatus.Authorized,
            };

            _byKey[key] = intent;
            _byId[intent.Id] = intent;
            return PaymentResult.Ok(intent);
        }
    }

    public PaymentResult Capture(Guid id, string? idempotencyKey) =>
        Transition(
            id,
            idempotencyKey,
            PaymentStatus.Captured,
            intent => intent.CaptureKey,
            (intent, key) => intent.CaptureKey = key,
            intent => intent.Status == PaymentStatus.Authorized
                || (intent.Status == PaymentStatus.Captured && intent.CaptureKey == idempotencyKey));

    public PaymentResult Void(Guid id, string? idempotencyKey) =>
        Transition(
            id,
            idempotencyKey,
            PaymentStatus.Voided,
            intent => intent.VoidKey,
            (intent, key) => intent.VoidKey = key,
            intent => intent.Status == PaymentStatus.Authorized
                || (intent.Status == PaymentStatus.Voided && intent.VoidKey == idempotencyKey));

    public PaymentResult Refund(Guid id, string? idempotencyKey) =>
        Transition(
            id,
            idempotencyKey,
            PaymentStatus.Refunded,
            intent => intent.RefundKey,
            (intent, key) => intent.RefundKey = key,
            intent => intent.Status == PaymentStatus.Captured
                || (intent.Status == PaymentStatus.Refunded && intent.RefundKey == idempotencyKey));

    public PaymentResult FindByKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return PaymentResult.Fail(PaymentError.MissingIdempotencyKey);
        }

        return _byKey.TryGetValue(key.Trim(), out var intent)
            ? PaymentResult.Ok(intent)
            : PaymentResult.Fail(PaymentError.NotFound);
    }

    public bool ShouldHang(bool isReplay)
    {
        if (isReplay)
        {
            return false;
        }

        var hang = options.Value;
        return hang.TimeoutHangMs > 0 && CoinFlip(hang.TimeoutRate);
    }

    public int LatencyMs => Math.Max(0, options.Value.LatencyMs);

    public int TimeoutHangMs => Math.Max(0, options.Value.TimeoutHangMs);

    private PaymentResult Transition(
        Guid id,
        string? idempotencyKey,
        PaymentStatus next,
        Func<PaymentIntent, string?> existingKey,
        Action<PaymentIntent, string> assignKey,
        Func<PaymentIntent, bool> allowed)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return PaymentResult.Fail(PaymentError.MissingIdempotencyKey);
        }

        var key = idempotencyKey.Trim();
        lock (_gate)
        {
            if (!_byId.TryGetValue(id, out var intent))
            {
                return PaymentResult.Fail(PaymentError.NotFound);
            }

            var prior = existingKey(intent);
            if (prior is not null)
            {
                return prior == key && intent.Status == next
                    ? PaymentResult.Ok(intent, isReplay: true)
                    : PaymentResult.Fail(PaymentError.IdempotencyKeyReused);
            }

            if (_byKey.ContainsKey(key))
            {
                return PaymentResult.Fail(PaymentError.IdempotencyKeyReused);
            }

            if (!allowed(intent))
            {
                return PaymentResult.Fail(PaymentError.InvalidState);
            }

            assignKey(intent, key);
            intent.Status = next;
            _byKey[key] = intent;
            return PaymentResult.Ok(intent);
        }
    }

    private static bool TryValidate(
        PaymentCommand command,
        string? idempotencyKey,
        out string key,
        out PaymentError error)
    {
        key = idempotencyKey?.Trim() ?? string.Empty;
        if (key.Length == 0)
        {
            error = PaymentError.MissingIdempotencyKey;
            return false;
        }

        if (command.Amount <= 0 || string.IsNullOrWhiteSpace(command.Currency))
        {
            error = PaymentError.InvalidAmount;
            return false;
        }

        error = PaymentError.None;
        return true;
    }

    private static string Hash(PaymentCommand command)
    {
        var payload =
            $"{command.Amount.ToString(CultureInfo.InvariantCulture)}|{command.Currency.Trim().ToUpperInvariant()}|{command.Description?.Trim()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static bool CoinFlip(decimal rate)
    {
        if (rate <= 0m)
        {
            return false;
        }

        if (rate >= 1m)
        {
            return true;
        }

        var threshold = (int)decimal.Round(rate * 10_000m, MidpointRounding.AwayFromZero);
        return Random.Shared.Next(10_000) < threshold;
    }
}
