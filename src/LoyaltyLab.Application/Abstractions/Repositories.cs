using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Pricing;
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

public interface IQuoteRepository
{
    Task<Quote?> GetByIdAsync(QuoteId id, CancellationToken cancellationToken);

    Task AddAsync(Quote quote, CancellationToken cancellationToken);
}

public interface IPricingRuleRepository
{
    Task<IReadOnlyList<PricingRule>> ListForPartnerAsync(
        PartnerId partnerId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);
}

public interface IPartnerSupplierRepository
{
    Task<IReadOnlySet<SupplierId>> GetPermittedSupplierIdsAsync(
        PartnerId partnerId,
        CancellationToken cancellationToken);
}

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(SupplierId id, CancellationToken cancellationToken);
}

public interface ILedgerRepository
{
    Task AddAccountAsync(LedgerAccount account, CancellationToken cancellationToken);

    Task<LedgerAccount?> FindAccountAsync(
        PartnerId partnerId,
        LedgerAccountType type,
        MemberId? memberId,
        CancellationToken cancellationToken);

    Task AddAsync(LedgerTransaction transaction, CancellationToken cancellationToken);

    Task<LedgerTransaction?> GetByIdAsync(LedgerTransactionId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerTransaction>> ListAsync(CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
