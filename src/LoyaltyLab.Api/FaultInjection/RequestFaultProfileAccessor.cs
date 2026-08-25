using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using Microsoft.Extensions.Options;

namespace LoyaltyLab.Api.FaultInjection;

/// <summary>
/// Per-request profile. Starts as the configured global profile; middleware may replace it.
/// </summary>
public sealed class RequestFaultProfileAccessor(IOptions<FaultProfile> global) : IFaultInjector
{
    private FaultProfile _current = global.Value;

    public FaultProfile Current => _current;

    public void Replace(FaultProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _current = profile;
    }
}
