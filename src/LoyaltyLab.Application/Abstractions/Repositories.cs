using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Idempotency;
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

    Task<LedgerAccount?> GetAccountAsync(LedgerAccountId id, CancellationToken cancellationToken);

    Task AddAsync(LedgerTransaction transaction, CancellationToken cancellationToken);

    Task<LedgerTransaction?> GetByIdAsync(LedgerTransactionId id, CancellationToken cancellationToken);

    Task<LedgerTransaction?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerTransaction>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerAccount>> ListAccountsAsync(CancellationToken cancellationToken);
}

public interface IBookingTenderQuery
{
    /// <summary>
    /// Independent total of settled booking credit tenders as of <paramref name="asOf"/>.
    /// Compared to ledger burns less reversals; never used to rewrite the ledger (FR-L-11).
    /// </summary>
    Task<int> SumSettledCreditTendersAsync(DateOnly asOf, CancellationToken cancellationToken);
}

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(
        PartnerId partnerId,
        string operation,
        string key,
        CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the record. Returns <see langword="false"/> if the unique key already exists.
    /// The database unique index is the source of truth under concurrent first-saves (FR-L-05).
    /// </summary>
    Task<bool> SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken);
}

public interface ISagaRepository
{
    Task AddAsync(SagaInstance saga, CancellationToken cancellationToken);

    Task<SagaInstance?> GetByIdAsync(SagaInstanceId id, CancellationToken cancellationToken);

    Task<SagaInstance?> GetByBookingIdAsync(BookingId bookingId, CancellationToken cancellationToken);
}

public interface IBookingRepository
{
    Task AddAsync(Domain.Booking.Booking booking, CancellationToken cancellationToken);

    Task<Domain.Booking.Booking?> GetByIdAsync(BookingId id, CancellationToken cancellationToken);
}

public interface IOutbox
{
    void Enqueue(OutboxMessage message);
}

/// <summary>
/// Handles a delivered outbox message. Delivery is at-least-once (FR-B-07),
/// so implementations must be idempotent on <see cref="OutboxMessage.Id"/>.
/// </summary>
public interface IOutboxHandler
{
    string MessageType { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
