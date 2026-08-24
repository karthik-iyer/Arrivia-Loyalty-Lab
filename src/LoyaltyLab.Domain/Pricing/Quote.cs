using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Pricing;

public enum RateDriftKind
{
    Unchanged = 0,
    Absorbed = 1,
}

public sealed record RateDriftOutcome(RateDriftKind Kind, Money? NetRateDelta)
{
    public static RateDriftOutcome Unchanged { get; } = new(RateDriftKind.Unchanged, null);

    public static RateDriftOutcome Absorbed(Money netRateDelta) => new(RateDriftKind.Absorbed, netRateDelta);
}

/// <summary>
/// An immutable priced snapshot (FR-P-06). Booking references this rather than recomputing.
/// </summary>
public sealed class Quote : Entity<QuoteId>, ITenantOwned
{
    private Quote()
    {
        Trace = [];
    }

    private Quote(
        QuoteId id,
        PartnerId partnerId,
        MemberId memberId,
        OfferId offerId,
        Money netRateSnapshot,
        Money netCostSnapshot,
        Money memberPrice,
        Money maxCreditTender,
        IReadOnlyList<PriceTraceEntry> trace,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
        : base(id)
    {
        PartnerId = partnerId;
        MemberId = memberId;
        OfferId = offerId;
        NetRateSnapshot = netRateSnapshot;
        NetCostSnapshot = netCostSnapshot;
        MemberPrice = memberPrice;
        MaxCreditTender = maxCreditTender;
        Trace = [.. trace];
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public PartnerId PartnerId { get; private set; }

    public MemberId MemberId { get; private set; }

    public OfferId OfferId { get; private set; }

    public Money NetRateSnapshot { get; private set; }

    public Money NetCostSnapshot { get; private set; }

    public Money MemberPrice { get; private set; }

    public Money MaxCreditTender { get; private set; }

    public List<PriceTraceEntry> Trace { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsExpired(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return clock.UtcNow >= ExpiresAt;
    }

    public static Quote Create(
        Member member,
        TravelOffer offer,
        PricingState state,
        QuotePolicy policy,
        IClock clock,
        QuoteId? id = null)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        if (state.IsRejected)
        {
            throw new DomainException("Cannot quote a rejected pricing run.");
        }

        if (!member.IsActive)
        {
            throw new DomainException("Inactive members cannot be quoted.");
        }

        var created = clock.UtcNow;
        var tender = state.MaxCreditTender ?? Money.Zero(state.RunningTotal.Currency);

        return new Quote(
            id ?? QuoteId.New(),
            member.PartnerId,
            member.Id,
            offer.Id,
            offer.NetRate,
            state.NetCost,
            state.RunningTotal,
            tender,
            state.Trace,
            created,
            created.AddMinutes(policy.ValidityMinutes));
    }

    /// <summary>
    /// Checkout re-check (FR-P-09, FR-P-11). Never silently reprices.
    /// </summary>
    public Result<RateDriftOutcome> Revalidate(
        TravelOffer currentOffer,
        QuotePolicy policy,
        Percent floorAboveNet,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(currentOffer);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        if (currentOffer.Id != OfferId)
        {
            throw new DomainException("Drift evaluation must use the quoted offer.");
        }

        if (IsExpired(clock))
        {
            return Result<RateDriftOutcome>.Failure(Errors.QuoteExpired);
        }

        var currentNet = currentOffer.NetRate;
        var currentNetCost = currentOffer.NetRate + currentOffer.TaxesAndFees;

        if (currentNet == NetRateSnapshot && currentNetCost == NetCostSnapshot)
        {
            return Result<RateDriftOutcome>.Success(RateDriftOutcome.Unchanged);
        }

        if (policy.DriftPolicy == RateDriftPolicy.RequoteRequired)
        {
            return Result<RateDriftOutcome>.Failure(Errors.RateChanged);
        }

        if (NetRateSnapshot.Amount == 0m)
        {
            throw new DomainException("Cannot compute drift against a zero net rate.");
        }

        var relative = Math.Abs((currentNet.Amount - NetRateSnapshot.Amount) / NetRateSnapshot.Amount);
        if (relative > policy.DriftTolerance.AsFraction())
        {
            return Result<RateDriftOutcome>.Failure(Errors.RateChanged);
        }

        var required = currentNetCost.ApplyPercent(floorAboveNet);
        if (MemberPrice < required)
        {
            return Result<RateDriftOutcome>.Failure(Errors.RateChanged);
        }

        return Result<RateDriftOutcome>.Success(RateDriftOutcome.Absorbed(currentNet - NetRateSnapshot));
    }
}
