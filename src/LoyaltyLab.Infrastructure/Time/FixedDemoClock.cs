using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Infrastructure.Time;

/// <summary>
/// Demo and test clock. Instant is configuration, never the machine clock (NFR-12).
/// </summary>
public sealed class FixedDemoClock : IClock
{
    public FixedDemoClock(DateTimeOffset utcNow) => UtcNow = utcNow;

    public DateTimeOffset UtcNow { get; }
}
