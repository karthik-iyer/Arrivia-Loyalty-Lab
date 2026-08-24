using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Tests;

internal sealed class MutableClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}
