using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Booking;

public sealed class SagaContext
{
    public required SagaInstance Saga { get; init; }

    public required Quote Quote { get; init; }

    public required TravelOffer Offer { get; init; }

    public required Partner Partner { get; init; }

    public required Member Member { get; init; }

    public required TenderSplit Tender { get; init; }

    public required DateOnly StayDate { get; init; }

    public required Percent FloorAboveNet { get; init; }

    public RateDriftOutcome? Drift { get; set; }

    public string Key(SagaStepKind kind) => SagaInstance.DeriveIdempotencyKey(Saga.Id, kind);

    public string CompensateKey(SagaStepKind kind) => SagaInstance.DeriveCompensationKey(Saga.Id, kind);

    public string? Reference(SagaStepKind kind) => Saga.Step(kind).ExternalReference;
}

public interface ISagaStep
{
    SagaStepKind Kind { get; }

    int Order { get; }

    Task<StepOutcome> ExecuteAsync(SagaContext context, CancellationToken cancellationToken);

    Task<CompensationOutcome> CompensateAsync(SagaContext context, CancellationToken cancellationToken);

    Task<StepOutcome> ResolveUnknownAsync(SagaContext context, CancellationToken cancellationToken);
}
