using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;

namespace LoyaltyLab.Infrastructure.Persistence.Outbox;

/// <summary>
/// Default production handler. A no-op is inherently idempotent on message id (FR-B-07).
/// </summary>
public sealed class NoOpOutboxHandler(string messageType) : IOutboxHandler
{
    public string MessageType { get; } = messageType;

    public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
