using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Booking;

public enum SagaStatus
{
    Running = 0,
    Compensating = 1,
    Confirmed = 2,
    Compensated = 3,
    RequiresManualReview = 4,
}

public enum SagaStepKind
{
    ValidateQuote = 0,
    ReserveInventory = 1,
    AuthorizePayment = 2,
    BurnCredits = 3,
    CapturePayment = 4,
    ConfirmBooking = 5,
}

public enum SagaStepStatus
{
    Pending = 0,
    InProgress = 1,
    Succeeded = 2,
    Failed = 3,
    Unknown = 4,
    Compensated = 5,
    CompensationFailed = 6,
}

public enum CompensationStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
}

public sealed record CompensationRecord(
    CompensationStatus Status,
    string? ExternalReference,
    Error? LastError,
    int Attempts,
    DateTimeOffset? CompletedAt);

/// <summary>
/// One step of a booking saga. Status is persisted before each external call (FR-B-02).
/// </summary>
public sealed class SagaStepRecord
{
    private SagaStepRecord()
    {
        IdempotencyKey = null!;
    }

    internal SagaStepRecord(SagaStepKind kind, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException("A saga step requires a derived idempotency key.");
        }

        Kind = kind;
        Status = SagaStepStatus.Pending;
        IdempotencyKey = idempotencyKey;
        Attempts = 0;
    }

    public SagaStepKind Kind { get; private set; }

    public SagaStepStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public string IdempotencyKey { get; private set; }

    public string? ExternalReference { get; private set; }

    public Error? LastError { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public CompensationRecord? Compensation { get; private set; }

    internal void BeginAttempt(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (Status is SagaStepStatus.Succeeded or SagaStepStatus.Compensated)
        {
            throw new DomainException($"Step {Kind} has already completed.");
        }

        Status = SagaStepStatus.InProgress;
        Attempts++;
        StartedAt ??= clock.UtcNow;
        LastError = null;
    }

    internal void RecordSucceeded(string? externalReference, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Status = SagaStepStatus.Succeeded;
        ExternalReference = externalReference;
        LastError = null;
        CompletedAt = clock.UtcNow;
    }

    internal void RecordUnknown(Error? error, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Status = SagaStepStatus.Unknown;
        LastError = error;
        CompletedAt = clock.UtcNow;
    }

    internal void RecordFailed(Error error, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(error);
        Status = SagaStepStatus.Failed;
        LastError = error;
        CompletedAt = clock.UtcNow;
    }
}

/// <summary>
/// Persisted booking saga. One instance per <see cref="BookingId"/>; <see cref="Version"/>
/// serializes concurrent writers (FR-B-12).
/// </summary>
public sealed class SagaInstance : Entity<SagaInstanceId>, ITenantOwned
{
    public const int StepCount = 6;

    private SagaInstance()
    {
        CorrelationId = null!;
        Steps = [];
    }

    private SagaInstance(
        SagaInstanceId id,
        PartnerId partnerId,
        BookingId bookingId,
        string correlationId,
        DateTimeOffset startedAt,
        List<SagaStepRecord> steps)
        : base(id)
    {
        PartnerId = partnerId;
        BookingId = bookingId;
        Status = SagaStatus.Running;
        CurrentStepIndex = 0;
        Steps = steps;
        CorrelationId = correlationId;
        StartedAt = startedAt;
        LastHeartbeatAt = startedAt;
        Version = 0;
    }

    public PartnerId PartnerId { get; private set; }

    public BookingId BookingId { get; private set; }

    public SagaStatus Status { get; private set; }

    public int CurrentStepIndex { get; private set; }

    public List<SagaStepRecord> Steps { get; private set; }

    public string CorrelationId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset LastHeartbeatAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public uint Version { get; private set; }

    /// <summary>
    /// Stable per (saga, step). Derived so a crash before persist still reproduces the key.
    /// </summary>
    public static string DeriveIdempotencyKey(SagaInstanceId sagaId, SagaStepKind kind) =>
        $"{sagaId.Value:D}:{kind}";

    public static string DeriveCompensationKey(SagaInstanceId sagaId, SagaStepKind kind) =>
        $"{sagaId.Value:D}:{kind}:compensate";

    public static SagaInstance Start(
        PartnerId partnerId,
        BookingId bookingId,
        string correlationId,
        IClock clock,
        SagaInstanceId? id = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new DomainException("A saga requires a correlation id.");
        }

        var sagaId = id ?? SagaInstanceId.New();
        var now = clock.UtcNow;
        var steps = Enum.GetValues<SagaStepKind>()
            .OrderBy(kind => (int)kind)
            .Select(kind => new SagaStepRecord(kind, DeriveIdempotencyKey(sagaId, kind)))
            .ToList();

        return new SagaInstance(sagaId, partnerId, bookingId, correlationId.Trim(), now, steps);
    }

    public SagaStepRecord Step(SagaStepKind kind) =>
        Steps.Single(step => step.Kind == kind);

    public SagaStepStatus StepStatus(SagaStepKind kind) => Step(kind).Status;

    public IReadOnlyList<SagaStepRecord> CompletedSteps =>
        [.. Steps.Where(step => step.Status == SagaStepStatus.Succeeded)];

    /// <summary>
    /// FR-B-02: persist InProgress before the external call.
    /// </summary>
    public void MarkInProgress(SagaStepKind kind, IClock clock)
    {
        EnsureRunning();
        Step(kind).BeginAttempt(clock);
        Touch(clock);
    }

    public void MarkSucceeded(SagaStepKind kind, string? externalReference, IClock clock)
    {
        EnsureRunning();
        Step(kind).RecordSucceeded(externalReference, clock);
        Touch(clock);
    }

    public void MarkUnknown(SagaStepKind kind, Error? error, IClock clock)
    {
        EnsureRunning();
        Step(kind).RecordUnknown(error, clock);
        Touch(clock);
    }

    public void MarkFailed(SagaStepKind kind, Error error, IClock clock)
    {
        EnsureRunning();
        Step(kind).RecordFailed(error, clock);
        Touch(clock);
    }

    public void Advance(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureRunning();
        var current = Steps[CurrentStepIndex];
        if (current.Status != SagaStepStatus.Succeeded)
        {
            throw new DomainException("A saga can only advance after the current step succeeded.");
        }

        if (CurrentStepIndex == Steps.Count - 1)
        {
            Status = SagaStatus.Confirmed;
            CompletedAt = clock.UtcNow;
        }
        else
        {
            CurrentStepIndex++;
        }

        Touch(clock);
    }

    private void EnsureRunning()
    {
        if (Status != SagaStatus.Running)
        {
            throw new DomainException($"Saga {Id} is {Status} and cannot accept a running-step mutation.");
        }
    }

    private void Touch(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        LastHeartbeatAt = clock.UtcNow;
        Version++;
    }
}
