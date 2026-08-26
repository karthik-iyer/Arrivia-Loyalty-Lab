using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class PartnerRepository(LoyaltyLabDbContext db) : IPartnerRepository
{
    public Task<Partner?> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
        db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, cancellationToken);

    public Task<Partner?> GetByIdAsync(PartnerId id, CancellationToken cancellationToken) =>
        db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Partner>> ListAsync(CancellationToken cancellationToken) =>
        await db.Partners.AsNoTracking().OrderBy(partner => partner.Code).ToListAsync(cancellationToken);
}

public sealed class MemberRepository(LoyaltyLabDbContext db) : IMemberRepository
{
    public Task<Member?> GetByIdAsync(MemberId id, CancellationToken cancellationToken) =>
        db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
}

public sealed class OfferRepository(LoyaltyLabDbContext db) : IOfferRepository
{
    public Task<TravelOffer?> GetByIdAsync(OfferId id, CancellationToken cancellationToken) =>
        db.Offers.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TravelOffer>> ListAsync(CancellationToken cancellationToken) =>
        await db.Offers.AsNoTracking().ToListAsync(cancellationToken);
}

public sealed class PartnerSupplierRepository(LoyaltyLabDbContext db) : IPartnerSupplierRepository
{
    public async Task<IReadOnlySet<SupplierId>> GetPermittedSupplierIdsAsync(
        PartnerId partnerId,
        CancellationToken cancellationToken)
    {
        var ids = await db.PartnerSuppliers
            .AsNoTracking()
            .Where(p => p.PartnerId == partnerId)
            .Select(p => p.SupplierId)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }
}

public sealed class PricingRuleRepository(LoyaltyLabDbContext db) : IPricingRuleRepository
{
    public async Task<IReadOnlyList<PricingRule>> ListForPartnerAsync(
        PartnerId partnerId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        var rules = await db.PricingRules
            .AsNoTracking()
            .Where(rule => rule.PartnerId == partnerId)
            .ToListAsync(cancellationToken);

        return [.. rules.Where(rule =>
            rule.EffectiveFrom <= asOf
            && (rule.EffectiveTo is not { } until || until > asOf))];
    }
}
