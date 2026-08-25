using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Application.Booking;

public sealed class ReserveInventoryStep(ISupplierClient supplier) : ISagaStep
{
    public SagaStepKind Kind => SagaStepKind.ReserveInventory;

    public int Order => (int)Kind;

    public Task<StepOutcome> ExecuteAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return supplier.ReserveAsync(
            new ReservationRequest(context.Offer.Id, context.StayDate, context.Key(Kind)),
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

        return MapCompensate(supplier.ReleaseAsync(reference, cancellationToken));
    }

    public Task<StepOutcome> ResolveUnknownAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return supplier.QueryReservationAsync(context.Key(Kind), cancellationToken);
    }

    private static async Task<CompensationOutcome> MapCompensate(Task<StepOutcome> outcome)
    {
        var result = await outcome;
        return result.Result == StepResult.Succeeded
            ? CompensationOutcome.Ok(result.ExternalReference)
            : CompensationOutcome.Fail(result.Error ?? Errors.SupplierUnavailable, result.ExternalReference);
    }
}
