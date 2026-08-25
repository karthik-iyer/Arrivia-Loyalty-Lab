namespace LoyaltyLab.Domain.Booking;

/// <summary>
/// Deliberate faults for a single request or a global demo configuration (FR-B-09).
/// Disabled unless the host enables injection, and refused in production (NFR-14).
/// </summary>
public sealed record FaultProfile(
    bool SupplierTimeout = false,
    bool SupplierDecline = false,
    bool PaymentTimeout = false,
    bool PaymentDecline = false,
    SagaStepKind? CrashAfterStep = null,
    int? AddedLatencyMs = null,
    bool PaymentCaptureDecline = false,
    bool SupplierReleaseFail = false)
{
    public FaultProfile()
        : this(false, false, false, false, null, null, false, false)
    {
    }

    public static FaultProfile None { get; } = new();
}
