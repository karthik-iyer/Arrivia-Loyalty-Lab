namespace LoyaltyLab.PaymentSim;

internal sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public int LatencyMs { get; set; }

    public decimal DeclineRate { get; set; }

    public decimal TimeoutRate { get; set; }

    public int TimeoutHangMs { get; set; }
}
