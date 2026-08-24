using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Ledger;

/// <summary>
/// Remaining credit lots after FIFO consumption. Expiry is applied to these lots as an explicit posting (FR-L-09).
/// </summary>
public sealed record CreditLot(int Remaining, DateTimeOffset OpenedAt, DateTimeOffset ExpiresAt);

public static class CreditLots
{
    public static IReadOnlyList<CreditLot> Remaining(
        IEnumerable<LedgerTransaction> history,
        LedgerAccountId memberCredits,
        int lifetimeDays)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (lifetimeDays <= 0)
        {
            throw new DomainException("Credit lifetime must be positive.");
        }

        var lots = new List<CreditLot>();
        foreach (var transaction in history.OrderBy(tx => tx.OccurredAt).ThenBy(tx => tx.Id.Value))
        {
            var delta = 0;
            foreach (var entry in transaction.Entries)
            {
                if (entry.AccountId == memberCredits)
                {
                    delta += entry.Amount;
                }
            }

            if (delta > 0)
            {
                lots.Add(new CreditLot(delta, transaction.OccurredAt, transaction.OccurredAt.AddDays(lifetimeDays)));
            }
            else if (delta < 0)
            {
                Consume(lots, -delta);
            }
        }

        return [.. lots.Where(lot => lot.Remaining > 0)];
    }

    public static int Due(IEnumerable<CreditLot> lots, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(lots);
        return lots.Where(lot => lot.ExpiresAt <= asOf).Sum(lot => lot.Remaining);
    }

    private static void Consume(List<CreditLot> lots, int amount)
    {
        for (var i = 0; i < lots.Count && amount > 0; i++)
        {
            var take = Math.Min(lots[i].Remaining, amount);
            lots[i] = lots[i] with { Remaining = lots[i].Remaining - take };
            amount -= take;
        }

        if (amount > 0)
        {
            throw new DomainException("FIFO consumption exceeded remaining credit lots.");
        }
    }
}
