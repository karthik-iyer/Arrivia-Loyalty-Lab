using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Application.Booking;

public sealed class AuthorizePaymentStep(IPaymentGateway payments) : ISagaStep
{
    public SagaStepKind Kind => SagaStepKind.AuthorizePayment;

    public int Order => (int)Kind;

    public Task<StepOutcome> ExecuteAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Tender.HasCash)
        {
            return Task.FromResult(StepOutcome.Succeeded());
        }

        return payments.AuthorizeAsync(
            new PaymentAuthorizeRequest(
                context.Tender.CashAmount,
                context.Key(Kind),
                $"Booking {context.Saga.BookingId.Value:D}"),
            cancellationToken);
    }

    public Task<CompensationOutcome> CompensateAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var reference = context.Reference(Kind);
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Task.FromResult(CompensationOutcome.Ok());
        }

        return PaymentCompensation.Map(
            payments.VoidAsync(
                new PaymentReferenceRequest(reference, context.CompensateKey(Kind)),
                cancellationToken));
    }

    public Task<StepOutcome> ResolveUnknownAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Tender.HasCash)
        {
            return Task.FromResult(StepOutcome.Succeeded());
        }

        return payments.QueryByKeyAsync(context.Key(Kind), cancellationToken);
    }
}

public sealed class CapturePaymentStep(IPaymentGateway payments) : ISagaStep
{
    public SagaStepKind Kind => SagaStepKind.CapturePayment;

    public int Order => (int)Kind;

    public Task<StepOutcome> ExecuteAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var paymentId = context.Reference(SagaStepKind.AuthorizePayment);
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return Task.FromResult(StepOutcome.Succeeded());
        }

        return payments.CaptureAsync(
            new PaymentReferenceRequest(paymentId, context.Key(Kind)),
            cancellationToken);
    }

    public Task<CompensationOutcome> CompensateAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var paymentId = context.Reference(Kind) ?? context.Reference(SagaStepKind.AuthorizePayment);
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return Task.FromResult(CompensationOutcome.Ok());
        }

        return PaymentCompensation.Map(
            payments.RefundAsync(
                new PaymentReferenceRequest(paymentId, context.CompensateKey(Kind)),
                cancellationToken));
    }

    public Task<StepOutcome> ResolveUnknownAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.Reference(SagaStepKind.AuthorizePayment)))
        {
            return Task.FromResult(StepOutcome.Succeeded());
        }

        return payments.QueryByKeyAsync(context.Key(Kind), cancellationToken);
    }
}

internal static class PaymentCompensation
{
    public static async Task<CompensationOutcome> Map(Task<StepOutcome> outcome)
    {
        var result = await outcome;
        return result.Result == StepResult.Succeeded
            ? CompensationOutcome.Ok(result.ExternalReference)
            : CompensationOutcome.Fail(result.Error ?? Errors.PaymentDeclined, result.ExternalReference);
    }
}
