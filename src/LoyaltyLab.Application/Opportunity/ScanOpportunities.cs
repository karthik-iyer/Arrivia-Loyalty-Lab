using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Opportunity;

/// <summary>
/// Evaluates members who have an availability feed, then refreshes the stalest watches
/// in a bounded batch so supplier volume tracks batch size (FR-O-03, FR-O-11).
/// </summary>
public sealed class ScanOpportunities(
    ITenantContextAccessor tenant,
    IClock clock,
    IPartnerRepository partners,
    IMemberRepository members,
    IOfferRepository offers,
    IPartnerSupplierRepository permits,
    IBusyPeriodRepository busyPeriods,
    IPriceWatchRepository watches,
    ISupplierClient suppliers,
    EvaluateOpportunities evaluate,
    IUnitOfWork unitOfWork) : IUseCase<ScanOpportunitiesCommand, ScanOpportunitiesResult>
{
    public async Task<Result<ScanOpportunitiesResult>> ExecuteAsync(
        ScanOpportunitiesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.WatchBatchSize <= 0)
        {
            throw new DomainException("Watch batch size must be positive.");
        }

        await EnsureWatchesAsync(cancellationToken);

        var membersScanned = 0;
        var nudgesWritten = 0;
        var busy = await busyPeriods.ListAllAsync(cancellationToken);
        foreach (var memberId in busy.Select(period => period.MemberId).Distinct())
        {
            var owner = busy.First(period => period.MemberId == memberId);
            tenant.Assign(TenantContext.Anonymous(owner.PartnerId));
            var member = await members.GetByIdAsync(memberId, cancellationToken);
            if (member is null)
            {
                continue;
            }

            tenant.Assign(TenantContext.ForMember(member));
            var result = await evaluate.ExecuteAsync(new EvaluateOpportunitiesCommand(), cancellationToken);
            if (result.IsFailure)
            {
                return Result<ScanOpportunitiesResult>.Failure(result.Error);
            }

            membersScanned++;
            nudgesWritten += result.Value.Nudges.Count;
        }

        var refreshed = await RefreshStaleAsync(request.WatchBatchSize, cancellationToken);
        return Result<ScanOpportunitiesResult>.Success(
            new ScanOpportunitiesResult(membersScanned, refreshed, nudgesWritten));
    }

    private async Task EnsureWatchesAsync(CancellationToken cancellationToken)
    {
        var catalog = await offers.ListAsync(cancellationToken);
        foreach (var partner in await partners.ListAsync(cancellationToken))
        {
            tenant.Assign(TenantContext.Anonymous(partner.Id));
            var permitted = await permits.GetPermittedSupplierIdsAsync(partner.Id, cancellationToken);
            var existing = (await watches.ListAsync(cancellationToken))
                .Select(watch => watch.OfferId)
                .ToHashSet();
            foreach (var offer in catalog.Where(item => permitted.Contains(item.SupplierId)))
            {
                if (existing.Contains(offer.Id))
                {
                    continue;
                }

                await watches.AddAsync(
                    PriceWatch.Open(partner.Id, offer.Id, offer.NetRate, clock),
                    cancellationToken);
                existing.Add(offer.Id);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> RefreshStaleAsync(int take, CancellationToken cancellationToken)
    {
        var stale = await watches.ListStaleAsync(take, cancellationToken);
        var refreshed = 0;
        foreach (var watch in stale)
        {
            tenant.Assign(TenantContext.Anonymous(watch.PartnerId));
            var live = await suppliers.GetCurrentNetRateAsync(watch.OfferId, cancellationToken);
            if (live.IsFailure)
            {
                continue;
            }

            watch.RecordCheck(live.Value, clock);
            refreshed++;
        }

        if (refreshed > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return refreshed;
    }
}
