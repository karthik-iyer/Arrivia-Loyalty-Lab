using LoyaltyLab.Application.Abstractions;

namespace LoyaltyLab.Infrastructure.Persistence;

/// <summary>
/// Booking tenders arrive in Phase 3. Until then the independent total is zero, so any burn
/// without a booking row is reported as a reconciliation gap rather than patched (FR-L-11).
/// </summary>
public sealed class BookingTenderQuery : IBookingTenderQuery
{
    public Task<int> SumSettledCreditTendersAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        _ = asOf;
        return Task.FromResult(0);
    }
}
