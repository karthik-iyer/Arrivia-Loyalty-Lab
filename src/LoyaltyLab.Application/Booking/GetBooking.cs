using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Booking;

public sealed class GetBooking(
    ITenantContextAccessor tenant,
    IBookingRepository bookings,
    ISagaRepository sagas) : IUseCase<GetBookingQuery, BookingResult>
{
    public async Task<Result<BookingResult>> ExecuteAsync(
        GetBookingQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var booking = await bookings.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null || !BookingVisibility.CanView(tenant.Current, booking.MemberId))
        {
            return Result<BookingResult>.Failure(Errors.BookingNotFound);
        }

        var saga = await sagas.GetByBookingIdAsync(booking.Id, cancellationToken);
        return saga is null
            ? Result<BookingResult>.Failure(Errors.BookingNotFound)
            : Result<BookingResult>.Success(BookingResult.From(booking, saga));
    }
}
