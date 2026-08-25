using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Booking;

public interface ISagaDelay
{
    Task DelayAsync(int attempt, CancellationToken cancellationToken);
}

public sealed class ImmediateSagaDelay : ISagaDelay
{
    public static ImmediateSagaDelay Instance { get; } = new();

    public Task DelayAsync(int attempt, CancellationToken cancellationToken)
    {
        _ = attempt;
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}

public sealed class ExponentialSagaDelay : ISagaDelay
{
    public static ExponentialSagaDelay Instance { get; } = new();

    public Task DelayAsync(int attempt, CancellationToken cancellationToken)
    {
        var exponent = Math.Clamp(attempt, 1, 8);
        var milliseconds = 100d * Math.Pow(2, exponent - 1);
        return Task.Delay(TimeSpan.FromMilliseconds(milliseconds), cancellationToken);
    }
}

/// <summary>
/// Drives a saga to a terminal state. Persists before every external call (FR-B-02).
/// Unknown parks the saga; the next advance resolves it (FR-B-04). Compensations run
/// in reverse completion order (FR-B-05).
/// </summary>
public sealed class AdvanceSaga(
    IReadOnlyList<ISagaStep> steps,
    IUnitOfWork unitOfWork,
    IClock clock,
    ISagaDelay delay,
    IOutbox outbox,
    IEnumerable<IFaultInjector>? faults = null)
{
    private readonly IFaultInjector? _faults = faults?.FirstOrDefault();

    public async Task<SagaStatus> ExecuteAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var saga = context.Saga;
        var byKind = Ordered(steps);
        var policy = context.Partner.SagaPolicy;

        if (saga.Status == SagaStatus.Compensating)
        {
            return await CompensateAsync(context, byKind, policy, cancellationToken);
        }

        if (saga.Status is SagaStatus.Confirmed or SagaStatus.Compensated or SagaStatus.RequiresManualReview)
        {
            return saga.Status;
        }

        while (saga.Status == SagaStatus.Running)
        {
            var step = byKind[saga.Steps[saga.CurrentStepIndex].Kind];
            var status = saga.StepStatus(step.Kind);
            var outcome = status is SagaStepStatus.Unknown or SagaStepStatus.InProgress
                ? await step.ResolveUnknownAsync(context, cancellationToken)
                : await ExecuteWithRetriesAsync(saga, step, context, policy, cancellationToken);

            switch (outcome.Result)
            {
                case StepResult.Succeeded:
                    saga.MarkSucceeded(step.Kind, outcome.ExternalReference, clock);
                    saga.Advance(clock);
                    if (step.Kind == SagaStepKind.BurnCredits)
                    {
                        Enqueue(OutboxMessageTypes.CreditsBurned, saga);
                    }

                    if (saga.Status == SagaStatus.Confirmed)
                    {
                        Enqueue(OutboxMessageTypes.BookingConfirmed, saga);
                    }

                    await PersistAsync(cancellationToken);
                    ThrowIfCrashRequested(step.Kind);
                    break;

                case StepResult.Unknown:
                    saga.MarkUnknown(step.Kind, outcome.Error, clock);
                    await PersistAsync(cancellationToken);
                    return saga.Status;

                case StepResult.Failed:
                    saga.MarkFailed(step.Kind, outcome.Error ?? Errors.SupplierUnavailable, clock);
                    await PersistAsync(cancellationToken);
                    return await CompensateAsync(context, byKind, policy, cancellationToken);

                default:
                    throw new DomainException($"Unhandled step result {outcome.Result}.");
            }
        }

        return saga.Status;
    }

    private async Task<StepOutcome> ExecuteWithRetriesAsync(
        SagaInstance saga,
        ISagaStep step,
        SagaContext context,
        SagaPolicy policy,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            saga.MarkInProgress(step.Kind, clock);
            await PersistAsync(cancellationToken);
            var outcome = await step.ExecuteAsync(context, cancellationToken);
            if (outcome.Result == StepResult.Failed
                && IsTransient(outcome.Error)
                && saga.Step(step.Kind).Attempts < policy.MaxStepAttempts)
            {
                await delay.DelayAsync(saga.Step(step.Kind).Attempts, cancellationToken);
                continue;
            }

            return outcome;
        }
    }

    private static bool IsTransient(Error? error) =>
        error is not null && error.Code == Errors.TemporaryFailure.Code;

    private async Task<SagaStatus> CompensateAsync(
        SagaContext context,
        Dictionary<SagaStepKind, ISagaStep> byKind,
        SagaPolicy policy,
        CancellationToken cancellationToken)
    {
        var saga = context.Saga;
        saga.BeginCompensation(clock);
        await PersistAsync(cancellationToken);

        foreach (var record in saga.CompletedSteps.Reverse().ToArray())
        {
            var step = byKind[record.Kind];
            CompensationOutcome? last = null;
            for (var attempt = 1; attempt <= policy.MaxCompensationAttempts; attempt++)
            {
                last = await step.CompensateAsync(context, cancellationToken);
                if (last.Succeeded)
                {
                    saga.MarkStepCompensated(
                        record.Kind,
                        new CompensationRecord(
                            CompensationStatus.Succeeded,
                            last.ExternalReference,
                            LastError: null,
                            attempt,
                            clock.UtcNow),
                        clock);
                    await PersistAsync(cancellationToken);
                    last = null;
                    break;
                }

                if (attempt < policy.MaxCompensationAttempts)
                {
                    await delay.DelayAsync(attempt, cancellationToken);
                }
            }

            if (last is { Succeeded: false })
            {
                saga.RequireManualReview(
                    record.Kind,
                    new CompensationRecord(
                        CompensationStatus.Failed,
                        last.ExternalReference,
                        last.Error,
                        policy.MaxCompensationAttempts,
                        clock.UtcNow),
                    clock);
                Enqueue(OutboxMessageTypes.BookingRequiresManualReview, saga);
                await PersistAsync(cancellationToken);
                return saga.Status;
            }
        }

        saga.CompleteCompensation(clock);
        Enqueue(OutboxMessageTypes.BookingCompensated, saga);
        await PersistAsync(cancellationToken);
        return saga.Status;
    }

    private void Enqueue(string type, SagaInstance saga) =>
        outbox.Enqueue(
            OutboxMessage.Create(
                saga.PartnerId,
                type,
                $$"""{"sagaId":"{{saga.Id.Value}}","bookingId":"{{saga.BookingId.Value}}"}""",
                saga.CorrelationId,
                clock));

    private void ThrowIfCrashRequested(SagaStepKind kind)
    {
        if (_faults?.Current.CrashAfterStep == kind)
        {
            throw new SimulatedCrashException(kind);
        }
    }

    private Task PersistAsync(CancellationToken cancellationToken) =>
        unitOfWork.SaveChangesAsync(cancellationToken);

    private static Dictionary<SagaStepKind, ISagaStep> Ordered(IReadOnlyList<ISagaStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count != SagaInstance.StepCount)
        {
            throw new DomainException($"A saga requires {SagaInstance.StepCount} steps, not {steps.Count}.");
        }

        return steps.ToDictionary(step => step.Kind);
    }
}
