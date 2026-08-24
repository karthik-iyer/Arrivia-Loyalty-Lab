namespace LoyaltyLab.PaymentSim;

internal interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// The only PaymentSim type allowed to read ambient time.
/// </summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
