using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;

namespace LoyaltyLab.Infrastructure.Persistence.Outbox;

public sealed class EfOutbox(LoyaltyLabDbContext db) : IOutbox
{
    public void Enqueue(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        db.OutboxMessages.Add(message);
    }
}
