namespace LoyaltyLab.Infrastructure.Persistence.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int MaxAttempts { get; set; } = 5;

    public int PollIntervalMs { get; set; } = 250;

    public OutboxDispatcherOptions Dispatcher { get; set; } = new();
}

public sealed class OutboxDispatcherOptions
{
    public bool Enabled { get; set; } = true;
}
