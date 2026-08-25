using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class SagaRepository(LoyaltyLabDbContext db) : ISagaRepository
{
    public Task AddAsync(SagaInstance saga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(saga);
        db.SagaInstances.Add(saga);
        return Task.CompletedTask;
    }

    public Task<SagaInstance?> GetByIdAsync(SagaInstanceId id, CancellationToken cancellationToken) =>
        db.SagaInstances.FirstOrDefaultAsync(saga => saga.Id == id, cancellationToken);

    public Task<SagaInstance?> GetByBookingIdAsync(BookingId bookingId, CancellationToken cancellationToken) =>
        db.SagaInstances.FirstOrDefaultAsync(saga => saga.BookingId == bookingId, cancellationToken);

    public async Task<IReadOnlyList<SagaInstance>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var rows = await db.SagaInstances
            .IgnoreQueryFilters()
            .Where(saga => saga.Status == SagaStatus.Running || saga.Status == SagaStatus.Compensating)
            .ToListAsync(cancellationToken);
        return rows;
    }
}
