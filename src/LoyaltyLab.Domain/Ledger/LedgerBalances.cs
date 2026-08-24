using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Ledger;

/// <summary>
/// Derived balances (FR-L-04). There is no stored balance column to drift from the entries.
/// </summary>
public static class LedgerBalances
{
    public static int For(LedgerAccountId accountId, IEnumerable<LedgerTransaction> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var total = 0;
        foreach (var transaction in history)
        {
            foreach (var entry in transaction.Entries)
            {
                if (entry.AccountId == accountId)
                {
                    total += entry.Amount;
                }
            }
        }

        return total;
    }

    public static IReadOnlyList<LedgerTransaction> OnOrBefore(
        IEnumerable<LedgerTransaction> history,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(history);

        return [.. history
            .Where(transaction => DateOnly.FromDateTime(transaction.OccurredAt.UtcDateTime) <= asOf)
            .OrderBy(transaction => transaction.OccurredAt)];
    }
}
