using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Infrastructure.Time;

/// <summary>
/// Production clock. The only production type allowed to read ambient time.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
