namespace LoyaltyLab.Domain.Common;

/// <summary>
/// The only source of "now" in the domain. Production uses a system clock;
/// tests and the demo use a fixed clock so expiry and effective dating are reproducible.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
