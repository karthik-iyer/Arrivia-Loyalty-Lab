using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoyaltyLab.Infrastructure.Persistence.Outbox;

/// <summary>
/// Polls undispatched rows in <c>OccurredAt</c> order, delivers at least once, and
/// moves exhausted messages to the poison table (FR-B-06, FR-B-07).
/// </summary>
public sealed class OutboxDispatcher(
    LoyaltyLabDbContext db,
    MutableTenantContextAccessor tenant,
    IClock clock,
    IEnumerable<IOutboxHandler> handlers,
    IOptions<OutboxOptions> options)
{
    public async Task DispatchAsync(CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, options.Value.MaxAttempts);
        var loaded = await db.OutboxMessages
            .IgnoreQueryFilters()
            .Where(message => message.DispatchedAt == null)
            .ToListAsync(cancellationToken);
        var pending = loaded
            .OrderBy(message => message.OccurredAt)
            .ToList();

        var byType = handlers
            .GroupBy(handler => handler.MessageType, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        foreach (var message in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tenant.Set(TenantContext.Anonymous(message.PartnerId));

            try
            {
                if (!byType.TryGetValue(message.Type, out var handler))
                {
                    throw new InvalidOperationException(
                        $"No IOutboxHandler is registered for '{message.Type}'.");
                }

                await handler.HandleAsync(message, cancellationToken);
                message.MarkDispatched(clock);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.RecordAttempt(Truncate(ex.Message));
                if (message.Attempts >= maxAttempts)
                {
                    db.PoisonMessages.Add(PoisonMessage.From(message, clock));
                    db.OutboxMessages.Remove(message);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string Truncate(string error)
    {
        var text = string.IsNullOrWhiteSpace(error) ? "Outbox delivery failed." : error.Trim();
        return text.Length <= 2000 ? text : text[..2000];
    }
}
