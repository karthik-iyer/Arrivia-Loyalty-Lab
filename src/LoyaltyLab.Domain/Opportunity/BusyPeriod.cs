using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Opportunity;

/// <summary>
/// A blocked span on a member's seeded availability feed. End is exclusive (FR-O-01).
/// </summary>
public sealed class BusyPeriod : Entity<BusyPeriodId>, ITenantOwned
{
    private BusyPeriod()
    {
    }

    private BusyPeriod(BusyPeriodId id, PartnerId partnerId, MemberId memberId, DateOnly start, DateOnly end)
        : base(id)
    {
        PartnerId = partnerId;
        MemberId = memberId;
        Start = start;
        End = end;
    }

    public PartnerId PartnerId { get; private set; }

    public MemberId MemberId { get; private set; }

    public DateOnly Start { get; private set; }

    public DateOnly End { get; private set; }

    public static BusyPeriod Create(
        PartnerId partnerId,
        MemberId memberId,
        DateOnly start,
        DateOnly end,
        BusyPeriodId? id = null)
    {
        if (end <= start)
        {
            throw new DomainException("A busy period needs an exclusive end after its start.");
        }

        return new BusyPeriod(id ?? BusyPeriodId.New(), partnerId, memberId, start, end);
    }
}
