using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class PoisonMessageQuery(LoyaltyLabDbContext db) : IPoisonMessageQuery
{
    public async Task<IReadOnlyList<PoisonMessage>> ListByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return [];
        }

        var rows = await db.PoisonMessages
            .Where(message => message.CorrelationId == correlationId.Trim())
            .ToListAsync(cancellationToken);
        return rows.OrderBy(message => message.PoisonedAt).ToList();
    }
}
