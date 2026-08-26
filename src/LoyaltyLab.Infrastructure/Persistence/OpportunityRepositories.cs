using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class BusyPeriodRepository(LoyaltyLabDbContext db) : IBusyPeriodRepository
{
    public async Task<IReadOnlyList<BusyPeriod>> ListForMemberAsync(
        MemberId memberId,
        CancellationToken cancellationToken)
    {
        var rows = await db.BusyPeriods
            .AsNoTracking()
            .Where(period => period.MemberId == memberId)
            .ToListAsync(cancellationToken);
        return [.. rows.OrderBy(period => period.Start)];
    }

    public async Task<IReadOnlyList<BusyPeriod>> ListAllAsync(CancellationToken cancellationToken)
    {
        var rows = await db.BusyPeriods
            .AsNoTracking()
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);
        return
        [
            .. rows
                .OrderBy(period => period.PartnerId.Value)
                .ThenBy(period => period.MemberId.Value)
                .ThenBy(period => period.Start),
        ];
    }
}

public sealed class NudgeRepository(LoyaltyLabDbContext db) : INudgeRepository
{
    public Task AddAsync(Nudge nudge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nudge);
        db.Nudges.Add(nudge);
        return Task.CompletedTask;
    }

    public Task<Nudge?> GetByIdAsync(NudgeId id, CancellationToken cancellationToken) =>
        db.Nudges.FirstOrDefaultAsync(nudge => nudge.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Nudge>> ListForMemberAsync(
        MemberId memberId,
        CancellationToken cancellationToken)
    {
        var rows = await db.Nudges
            .AsNoTracking()
            .Where(nudge => nudge.MemberId == memberId)
            .ToListAsync(cancellationToken);
        return [.. rows.OrderByDescending(nudge => nudge.CreatedAt)];
    }
}

public sealed class PriceWatchRepository(LoyaltyLabDbContext db) : IPriceWatchRepository
{
    public Task AddAsync(PriceWatch watch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(watch);
        db.PriceWatches.Add(watch);
        return Task.CompletedTask;
    }

    public Task<PriceWatch?> FindByOfferAsync(OfferId offerId, CancellationToken cancellationToken) =>
        db.PriceWatches.AsNoTracking().FirstOrDefaultAsync(watch => watch.OfferId == offerId, cancellationToken);

    public async Task<IReadOnlyList<PriceWatch>> ListAsync(CancellationToken cancellationToken) =>
        await db.PriceWatches.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PriceWatch>> ListStaleAsync(int take, CancellationToken cancellationToken)
    {
        var rows = await db.PriceWatches
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);
        return
        [
            .. rows
                .OrderBy(watch => watch.LastCheckedAt)
                .ThenBy(watch => watch.Id.Value)
                .Take(take),
        ];
    }
}
