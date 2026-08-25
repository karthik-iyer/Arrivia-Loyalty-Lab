using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Application.Booking;

public sealed class ValidateQuoteStep(ISupplierClient supplier, IClock clock) : ISagaStep
{
    public SagaStepKind Kind => SagaStepKind.ValidateQuote;

    public int Order => (int)Kind;

    public async Task<StepOutcome> ExecuteAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var live = await supplier.GetCurrentNetRateAsync(context.Offer.Id, cancellationToken);
        if (live.IsFailure)
        {
            return StepOutcome.Failed(live.Error);
        }

        var current = live.Value == context.Offer.NetRate
            ? context.Offer
            : TravelOffer.Create(
                context.Offer.SupplierId,
                context.Offer.PropertyName,
                context.Offer.Destination,
                live.Value,
                context.Offer.TaxesAndFees,
                context.Offer.Tags,
                context.Offer.StarRating,
                context.Offer.AvailableFrom,
                context.Offer.AvailableTo,
                context.Offer.Id);

        var drift = context.Quote.Revalidate(
            current,
            context.Partner.QuotePolicy,
            context.FloorAboveNet,
            clock);
        if (drift.IsFailure)
        {
            return StepOutcome.Failed(drift.Error);
        }

        context.Drift = drift.Value;
        return StepOutcome.Succeeded();
    }

    public Task<CompensationOutcome> CompensateAsync(SagaContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return Task.FromResult(CompensationOutcome.Ok());
    }

    public Task<StepOutcome> ResolveUnknownAsync(SagaContext context, CancellationToken cancellationToken) =>
        ExecuteAsync(context, cancellationToken);
}
