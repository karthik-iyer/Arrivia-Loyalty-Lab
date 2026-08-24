using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Abstractions;

public interface IPartnerRepository
{
    Task<Partner?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<Partner?> GetByIdAsync(PartnerId id, CancellationToken cancellationToken);
}

public interface IMemberRepository
{
    Task<Member?> GetByIdAsync(MemberId id, CancellationToken cancellationToken);
}

public interface IOfferRepository
{
    Task<TravelOffer?> GetByIdAsync(OfferId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TravelOffer>> ListAsync(CancellationToken cancellationToken);
}

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(SupplierId id, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
