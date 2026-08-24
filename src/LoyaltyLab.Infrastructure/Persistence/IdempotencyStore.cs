using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Idempotency;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class IdempotencyStore(LoyaltyLabDbContext db) : IIdempotencyStore
{
    public Task<IdempotencyRecord?> FindAsync(
        PartnerId partnerId,
        string operation,
        string key,
        CancellationToken cancellationToken) =>
        db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                record => record.PartnerId == partnerId && record.Operation == operation && record.Key == key,
                cancellationToken);

    public async Task<bool> SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        db.IdempotencyRecords.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            db.Entry(record).State = EntityState.Detached;
            return false;
        }
    }

    private static bool IsUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException is SqliteException sqlite && sqlite.SqliteErrorCode == 19;
}
