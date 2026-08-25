namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class SagaRecoveryOptions
{
    public const string SectionName = "SagaRecovery";

    public int PollIntervalMs { get; set; } = 1_000;

    public bool Enabled { get; set; } = true;
}
