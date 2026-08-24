using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Application.Abstractions;

public sealed record ReservationRequest(OfferId OfferId, DateOnly StayDate, string IdempotencyKey);

public interface ISupplierClient
{
    Task<Result<Money>> GetCurrentNetRateAsync(OfferId offerId, CancellationToken cancellationToken);

    Task<StepOutcome> ReserveAsync(ReservationRequest request, CancellationToken cancellationToken);

    Task<StepOutcome> ReleaseAsync(string reference, CancellationToken cancellationToken);

    Task<StepOutcome> QueryReservationAsync(string idempotencyKey, CancellationToken cancellationToken);
}
