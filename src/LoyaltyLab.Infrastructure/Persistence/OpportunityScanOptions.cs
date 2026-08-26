namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class OpportunityScanOptions
{
    public const string SectionName = "OpportunityScan";

    public bool Enabled { get; set; }

    public int PollIntervalMs { get; set; } = 60_000;

    public int BatchSize { get; set; } = 10;
}
