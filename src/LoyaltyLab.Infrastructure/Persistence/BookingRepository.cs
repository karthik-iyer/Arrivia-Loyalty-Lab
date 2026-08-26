using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class BookingRepository(LoyaltyLabDbContext db) : IBookingRepository
{
    public Task AddAsync(Booking booking, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);
        db.Bookings.Add(booking);
        return Task.CompletedTask;
    }

    public Task<Booking?> GetByIdAsync(BookingId id, CancellationToken cancellationToken) =>
        db.Bookings.FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);

    public Task<Booking?> FindByQuoteIdAsync(QuoteId quoteId, CancellationToken cancellationToken) =>
        db.Bookings.FirstOrDefaultAsync(booking => booking.QuoteId == quoteId, cancellationToken);

    public async Task<IReadOnlyList<Booking>> ListForMemberAsync(
        MemberId memberId,
        CancellationToken cancellationToken) =>
        await db.Bookings
            .Where(booking => booking.MemberId == memberId)
            .ToListAsync(cancellationToken);
}
