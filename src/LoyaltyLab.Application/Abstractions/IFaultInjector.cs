using LoyaltyLab.Domain.Booking;

namespace LoyaltyLab.Application.Abstractions;

/// <summary>
/// Current fault profile for this scope. Registered only when
/// <c>Features:FaultInjection</c> is enabled (NFR-14).
/// </summary>
public interface IFaultInjector
{
    FaultProfile Current { get; }
}
