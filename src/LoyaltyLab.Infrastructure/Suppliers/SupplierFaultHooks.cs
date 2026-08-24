namespace LoyaltyLab.Infrastructure.Suppliers;

/// <summary>
/// In-process fault switches for the simulated supplier (FR-B-09).
/// T-038 will populate these from <c>X-Fault-Profile</c>; tests set them directly.
/// </summary>
public sealed class SupplierFaultHooks
{
    public bool TimeoutOnReserve { get; set; }

    public bool DeclineOnReserve { get; set; }

    public int AddedLatencyMs { get; set; }
}
