using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Booking;

public enum StepResult
{
    Succeeded = 0,
    Failed = 1,
    Unknown = 2,
}

/// <summary>
/// Outcome of a saga step or an external call. Timeout is <see cref="StepResult.Unknown"/>,
/// never <see cref="StepResult.Failed"/> (FR-B-04).
/// </summary>
public sealed record StepOutcome(StepResult Result, string? ExternalReference, Error? Error)
{
    public static StepOutcome Succeeded(string? externalReference = null) =>
        new(StepResult.Succeeded, externalReference, Error: null);

    public static StepOutcome Failed(Error error, string? externalReference = null) =>
        new(StepResult.Failed, externalReference, error);

    public static StepOutcome Unknown() =>
        new(StepResult.Unknown, ExternalReference: null, Error: null);
}
