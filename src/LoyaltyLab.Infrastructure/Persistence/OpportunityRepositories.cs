using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class BusyPeriodRepository(LoyaltyLabDbContext db) : IBusyPeriodRepository
{
    public async Task<IReadOnlyList<BusyPeriod>> ListForMemberAsync(
        MemberId memberId,
        CancellationToken cancellationToken) =>
        await db.BusyPeriods
            .AsNoTracking()
            .Where(period => period.MemberId == memberId)
            .OrderBy(period => period.Start)
            .ToListAsync(cancellationToken);
}

public sealed class NudgeRepository(LoyaltyLabDbContext db) : INudgeRepository
{
    public Task AddAsync(Nudge nudge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nudge);
        db.Nudges.Add(nudge);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Nudge>> ListForMemberAsync(
        MemberId memberId,
        CancellationToken cancellationToken) =>
        await db.Nudges
            .AsNoTracking()
            .Where(nudge => nudge.MemberId == memberId)
            .OrderByDescending(nudge => nudge.CreatedAt)
            .ToListAsync(cancellationToken);
}

public sealed class PriceWatchRepository(LoyaltyLabDbContext db) : IPriceWatchRepository
{
    public Task<PriceWatch?> FindByOfferAsync(OfferId offerId, CancellationToken cancellationToken) =>
        db.PriceWatches.AsNoTracking().FirstOrDefaultAsync(watch => watch.OfferId == offerId, cancellationToken);

    public async Task<IReadOnlyList<PriceWatch>> ListAsync(CancellationToken cancellationToken) =>
        await db.PriceWatches.AsNoTracking().ToListAsync(cancellationToken);
}
