using LoyaltyLab.Domain.Booking;

namespace LoyaltyLab.Application.Booking;

/// <summary>
/// Marker thrown after a named saga step is persisted. The host treats it as a
/// process abort so recovery can be demonstrated (FR-B-09).
/// </summary>
public sealed class SimulatedCrashException : Exception
{
    public SimulatedCrashException(SagaStepKind afterStep)
        : base($"Simulated crash after {afterStep}.")
    {
        AfterStep = afterStep;
    }

    public SagaStepKind AfterStep { get; }
}
