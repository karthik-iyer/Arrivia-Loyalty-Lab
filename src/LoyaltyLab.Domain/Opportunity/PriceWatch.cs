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
        if (baselineNetRate.IsNegative)
        {
            throw new DomainException("A price-watch baseline cannot be negative.");
        }

        return new PriceWatch(id ?? PriceWatchId.New(), partnerId, offerId, baselineNetRate, clock.UtcNow);
    }

    /// <summary>
    /// Rolling baseline: a permanently cheap offer stops registering as a drop (FR-O-03, FR-O-11).
    /// </summary>
    public void RecordCheck(Money currentNet, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (currentNet.Currency != BaselineNetRate.Currency)
        {
            throw new DomainException("A price-watch check must use the baseline currency.");
        }

        if (currentNet.IsNegative)
        {
            throw new DomainException("A live net rate cannot be negative.");
        }

        BaselineNetRate = currentNet;
        LastCheckedAt = clock.UtcNow;
    }
}
