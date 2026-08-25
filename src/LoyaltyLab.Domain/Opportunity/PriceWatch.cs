using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Opportunity;

/// <summary>
/// Baseline net rate for drop detection. The scan worker (T-073) refreshes <see cref="LastCheckedAt"/>.
/// </summary>
public sealed class PriceWatch : Entity<PriceWatchId>, ITenantOwned
{
    private PriceWatch()
    {
    }

    private PriceWatch(
        PriceWatchId id,
        PartnerId partnerId,
        OfferId offerId,
        Money baselineNetRate,
        DateTimeOffset lastCheckedAt)
        : base(id)
    {
        PartnerId = partnerId;
        OfferId = offerId;
        BaselineNetRate = baselineNetRate;
        LastCheckedAt = lastCheckedAt;
    }

    public PartnerId PartnerId { get; private set; }

    public OfferId OfferId { get; private set; }

    public Money BaselineNetRate { get; private set; }

    public DateTimeOffset LastCheckedAt { get; private set; }

    public static PriceWatch Open(PartnerId partnerId, OfferId offerId, Money baselineNetRate, IClock clock, PriceWatchId? id = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        return new PriceWatch(id ?? PriceWatchId.New(), partnerId, offerId, baselineNetRate, clock.UtcNow);
    }
}
