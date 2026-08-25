using LoyaltyLab.Domain.Booking;

namespace LoyaltyLab.Infrastructure.Suppliers;

/// <summary>
/// In-process fault switches for the simulated supplier (FR-B-09).
/// The API applies <see cref="FaultProfile"/> from <c>X-Fault-Profile</c> when
/// injection is enabled; tests may set these directly.
/// </summary>
public sealed class SupplierFaultHooks
{
    public bool TimeoutOnReserve { get; set; }

    public bool DeclineOnReserve { get; set; }

    public bool FailOnRelease { get; set; }

    public int AddedLatencyMs { get; set; }

    public void Apply(FaultProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        TimeoutOnReserve = profile.SupplierTimeout;
        DeclineOnReserve = profile.SupplierDecline;
        FailOnRelease = profile.SupplierReleaseFail;
        AddedLatencyMs = Math.Max(0, profile.AddedLatencyMs ?? 0);
    }
}
