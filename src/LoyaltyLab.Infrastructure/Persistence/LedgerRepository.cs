using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class LedgerRepository(LoyaltyLabDbContext db) : ILedgerRepository
{
    public Task AddAccountAsync(LedgerAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        db.LedgerAccounts.Add(account);
        return Task.CompletedTask;
    }

    public Task<LedgerAccount?> FindAccountAsync(
        PartnerId partnerId,
        LedgerAccountType type,
        MemberId? memberId,
        CancellationToken cancellationToken) =>
        db.LedgerAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                account => account.PartnerId == partnerId && account.Type == type && account.MemberId == memberId,
                cancellationToken);

    public Task<LedgerAccount?> GetAccountAsync(LedgerAccountId id, CancellationToken cancellationToken) =>
        db.LedgerAccounts.AsNoTracking().FirstOrDefaultAsync(account => account.Id == id, cancellationToken);

    public Task AddAsync(LedgerTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        db.LedgerTransactions.Add(transaction);
        return Task.CompletedTask;
    }

    public Task<LedgerTransaction?> GetByIdAsync(LedgerTransactionId id, CancellationToken cancellationToken) =>
        db.LedgerTransactions.FirstOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);

    public Task<LedgerTransaction?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        db.LedgerTransactions.FirstOrDefaultAsync(
            transaction => transaction.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public async Task<IReadOnlyList<LedgerTransaction>> ListAsync(CancellationToken cancellationToken)
    {
        var loaded = await db.LedgerTransactions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. loaded.OrderBy(transaction => transaction.OccurredAt)];
    }
}
