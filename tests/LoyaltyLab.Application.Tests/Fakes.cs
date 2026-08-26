using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Idempotency;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Opportunity;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Tests;

internal sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}

internal sealed class FakeTenant : ITenantContextAccessor
{
    public TenantContext Current { get; set; } = null!;

    public bool HasCurrent => Current is not null;

    public void Set(TenantContext context) => Current = context;

    public void Assign(TenantContext context) => Set(context);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeOffers(params TravelOffer[] offers) : IOfferRepository
{
    private readonly List<TravelOffer> _offers = [.. offers];

    public Task<TravelOffer?> GetByIdAsync(OfferId id, CancellationToken cancellationToken) =>
        Task.FromResult(_offers.SingleOrDefault(o => o.Id == id));

    public Task<IReadOnlyList<TravelOffer>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TravelOffer>>(_offers);
}

internal sealed class FakeMembers(params Member[] members) : IMemberRepository
{
    public Task<Member?> GetByIdAsync(MemberId id, CancellationToken cancellationToken) =>
        Task.FromResult(members.SingleOrDefault(m => m.Id == id));
}

internal sealed class FakePartners(params Partner[] partners) : IPartnerRepository
{
    public Task<Partner?> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(partners.SingleOrDefault(p => p.Code == code));

    public Task<Partner?> GetByIdAsync(PartnerId id, CancellationToken cancellationToken) =>
        Task.FromResult(partners.SingleOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Partner>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Partner>>(partners);
}

internal sealed class FakeRules(params PricingRule[] rules) : IPricingRuleRepository
{
    public Task<IReadOnlyList<PricingRule>> ListForPartnerAsync(
        PartnerId partnerId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PricingRule>>([.. rules.Where(r => r.PartnerId == partnerId)]);
}

internal sealed class FakePermits : IPartnerSupplierRepository
{
    private readonly Dictionary<PartnerId, HashSet<SupplierId>> _permits = [];

    public void Allow(PartnerId partnerId, params SupplierId[] suppliers) =>
        _permits[partnerId] = [.. suppliers];

    public Task<IReadOnlySet<SupplierId>> GetPermittedSupplierIdsAsync(
        PartnerId partnerId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<SupplierId>>(
            _permits.TryGetValue(partnerId, out var set) ? set : new HashSet<SupplierId>());
}

internal sealed class FakeQuotes(ITenantContextAccessor tenant) : IQuoteRepository
{
    private readonly List<Quote> _quotes = [];

    public IReadOnlyList<Quote> Items => _quotes;

    public Task AddAsync(Quote quote, CancellationToken cancellationToken)
    {
        _quotes.Add(quote);
        return Task.CompletedTask;
    }

    public Task<Quote?> GetByIdAsync(QuoteId id, CancellationToken cancellationToken)
    {
        var quote = _quotes.SingleOrDefault(q => q.Id == id);
        if (quote is null || quote.PartnerId != tenant.Current.PartnerId)
        {
            return Task.FromResult<Quote?>(null);
        }

        return Task.FromResult<Quote?>(quote);
    }
}

internal sealed class FakeSagas : ISagaRepository
{
    public List<SagaInstance> Items { get; } = [];

    public Task AddAsync(SagaInstance saga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(saga);
        Items.Add(saga);
        return Task.CompletedTask;
    }

    public Task<SagaInstance?> GetByIdAsync(SagaInstanceId id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(saga => saga.Id == id));

    public Task<SagaInstance?> GetByBookingIdAsync(BookingId bookingId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(saga => saga.BookingId == bookingId));

    public Task<IReadOnlyList<SagaInstance>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SagaInstance>>(
            [.. Items
                .OrderByDescending(saga => saga.Status == SagaStatus.RequiresManualReview)
                .ThenByDescending(saga => saga.StartedAt)]);

    public Task<IReadOnlyList<SagaInstance>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SagaInstance>>(
            [.. Items.Where(saga => saga.Status is SagaStatus.Running or SagaStatus.Compensating)]);
}

internal sealed class FakeIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<(Guid Partner, string Operation, string Key), IdempotencyRecord> _records = [];

    public Task<IdempotencyRecord?> FindAsync(
        PartnerId partnerId,
        string operation,
        string key,
        CancellationToken cancellationToken)
    {
        _records.TryGetValue((partnerId.Value, operation, key), out var record);
        return Task.FromResult(record);
    }

    public Task<bool> SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Task.FromResult(_records.TryAdd((record.PartnerId.Value, record.Operation, record.Key), record));
    }
}

internal sealed class FakeLedger : ILedgerRepository
{
    private readonly List<LedgerAccount> _accounts = [];
    private readonly List<LedgerTransaction> _transactions = [];

    public IReadOnlyList<LedgerTransaction> Transactions => _transactions;

    public Task AddAccountAsync(LedgerAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        _accounts.Add(account);
        return Task.CompletedTask;
    }

    public Task<LedgerAccount?> FindAccountAsync(
        PartnerId partnerId,
        LedgerAccountType type,
        MemberId? memberId,
        CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.SingleOrDefault(account =>
            account.PartnerId == partnerId && account.Type == type && account.MemberId == memberId));

    public Task<LedgerAccount?> GetAccountAsync(LedgerAccountId id, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.SingleOrDefault(account => account.Id == id));

    public Task AddAsync(LedgerTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        _transactions.Add(transaction);
        return Task.CompletedTask;
    }

    public Task<LedgerTransaction?> GetByIdAsync(LedgerTransactionId id, CancellationToken cancellationToken) =>
        Task.FromResult(_transactions.SingleOrDefault(transaction => transaction.Id == id));

    public Task<LedgerTransaction?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        Task.FromResult(_transactions.SingleOrDefault(transaction => transaction.IdempotencyKey == idempotencyKey));

    public Task<IReadOnlyList<LedgerTransaction>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LedgerTransaction>>([.. _transactions]);

    public Task<IReadOnlyList<LedgerAccount>> ListAccountsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LedgerAccount>>([.. _accounts]);
}

internal sealed class FakeBookingTenders : IBookingTenderQuery
{
    public int Tenders { get; set; }

    public Task<int> SumSettledCreditTendersAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        _ = asOf;
        return Task.FromResult(Tenders);
    }
}

internal sealed class FakePoison : IPoisonMessageQuery
{
    public List<PoisonMessage> Items { get; } = [];

    public Task<IReadOnlyList<PoisonMessage>> ListByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PoisonMessage>>(
            [.. Items.Where(message => message.CorrelationId == correlationId)]);
}

internal sealed class FakeOutboxDispatch : IOutboxDispatch
{
    public int Processed { get; set; }

    public Task<int> DispatchAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(Processed);
    }
}

internal sealed class FakeOutbox : IOutbox
{
    public List<OutboxMessage> Messages { get; } = [];

    public void Enqueue(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Messages.Add(message);
    }
}

internal sealed class FakeBookings : IBookingRepository
{
    private readonly List<LoyaltyLab.Domain.Booking.Booking> _bookings = [];

    public IReadOnlyList<LoyaltyLab.Domain.Booking.Booking> Items => _bookings;

    public Task AddAsync(LoyaltyLab.Domain.Booking.Booking booking, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);
        _bookings.Add(booking);
        return Task.CompletedTask;
    }

    public Task<LoyaltyLab.Domain.Booking.Booking?> GetByIdAsync(BookingId id, CancellationToken cancellationToken) =>
        Task.FromResult(_bookings.SingleOrDefault(booking => booking.Id == id));

    public Task<LoyaltyLab.Domain.Booking.Booking?> FindByQuoteIdAsync(
        QuoteId quoteId,
        CancellationToken cancellationToken) =>
        Task.FromResult(_bookings.SingleOrDefault(booking => booking.QuoteId == quoteId));

    public Task<IReadOnlyList<LoyaltyLab.Domain.Booking.Booking>> ListForMemberAsync(
        MemberId memberId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LoyaltyLab.Domain.Booking.Booking>>(
            [.. _bookings.Where(booking => booking.MemberId == memberId)]);
}

internal sealed class FakeBusyPeriods : IBusyPeriodRepository
{
    private readonly List<BusyPeriod> _items = [];

    public void Add(BusyPeriod period) => _items.Add(period);

    public Task<IReadOnlyList<BusyPeriod>> ListForMemberAsync(
        MemberId memberId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BusyPeriod>>([.. _items.Where(period => period.MemberId == memberId)]);

    public Task<IReadOnlyList<BusyPeriod>> ListAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BusyPeriod>>(_items);
}

internal sealed class FakeNudges : INudgeRepository
{
    public List<Nudge> Items { get; } = [];

    public Task AddAsync(Nudge nudge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nudge);
        Items.Add(nudge);
        return Task.CompletedTask;
    }

    public Task<Nudge?> GetByIdAsync(NudgeId id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(nudge => nudge.Id == id));

    public Task<IReadOnlyList<Nudge>> ListForMemberAsync(
        MemberId memberId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Nudge>>([.. Items.Where(nudge => nudge.MemberId == memberId)]);
}

internal sealed class FakePriceWatches : IPriceWatchRepository
{
    private readonly List<PriceWatch> _items = [];

    public IReadOnlyList<PriceWatch> Items => _items;

    public void Add(PriceWatch watch) => _items.Add(watch);

    public Task AddAsync(PriceWatch watch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(watch);
        _items.Add(watch);
        return Task.CompletedTask;
    }

    public Task<PriceWatch?> FindByOfferAsync(OfferId offerId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.SingleOrDefault(watch => watch.OfferId == offerId));

    public Task<IReadOnlyList<PriceWatch>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PriceWatch>>(_items);

    public Task<IReadOnlyList<PriceWatch>> ListStaleAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PriceWatch>>(
            [.. _items.OrderBy(watch => watch.LastCheckedAt).ThenBy(watch => watch.Id.Value).Take(take)]);
}

internal sealed class FakeSupplier : ISupplierClient
{
    public Result<Money> NetRate { get; set; } = Result<Money>.Failure(Errors.OfferNotFound);

    public Dictionary<OfferId, Result<Money>> Rates { get; } = [];

    public int RateCalls { get; private set; }

    public StepOutcome Reserve { get; set; } = StepOutcome.Succeeded("res-1");

    public StepOutcome Release { get; set; } = StepOutcome.Succeeded("res-1");

    public StepOutcome Query { get; set; } = StepOutcome.Succeeded("res-1");

    public string? LastReleased { get; private set; }

    public Action? OnReserve { get; set; }

    public Func<int, StepOutcome>? ReserveOnCall { get; set; }

    public Func<int, StepOutcome>? ReleaseOnCall { get; set; }

    public int ReserveCalls { get; private set; }

    public int ReleaseCalls { get; private set; }

    public Task<Result<Money>> GetCurrentNetRateAsync(OfferId offerId, CancellationToken cancellationToken)
    {
        RateCalls++;
        if (Rates.TryGetValue(offerId, out var rate))
        {
            return Task.FromResult(rate);
        }

        return Task.FromResult(NetRate);
    }

    public Task<StepOutcome> ReserveAsync(ReservationRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        OnReserve?.Invoke();
        ReserveCalls++;
        return Task.FromResult(ReserveOnCall?.Invoke(ReserveCalls) ?? Reserve);
    }

    public Task<StepOutcome> ReleaseAsync(string reference, CancellationToken cancellationToken)
    {
        LastReleased = reference;
        ReleaseCalls++;
        return Task.FromResult(ReleaseOnCall?.Invoke(ReleaseCalls) ?? Release);
    }

    public Task<StepOutcome> QueryReservationAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        _ = idempotencyKey;
        return Task.FromResult(Query);
    }
}

internal sealed class FakePayments : IPaymentGateway
{
    public StepOutcome Authorize { get; set; } = StepOutcome.Succeeded("pay-1");

    public StepOutcome Capture { get; set; } = StepOutcome.Succeeded("pay-1");

    public StepOutcome Void { get; set; } = StepOutcome.Succeeded("pay-1");

    public StepOutcome Refund { get; set; } = StepOutcome.Succeeded("pay-1");

    public StepOutcome Query { get; set; } = StepOutcome.Succeeded("pay-1");

    public string? LastAuthorizeKey { get; private set; }

    public string? LastQueryKey { get; private set; }

    public string? LastVoidId { get; private set; }

    public string? LastCaptureId { get; private set; }

    public string? LastRefundId { get; private set; }

    public Task<StepOutcome> AuthorizeAsync(PaymentAuthorizeRequest request, CancellationToken cancellationToken)
    {
        LastAuthorizeKey = request.IdempotencyKey;
        return Task.FromResult(Authorize);
    }

    public Task<StepOutcome> CaptureAsync(PaymentReferenceRequest request, CancellationToken cancellationToken)
    {
        LastCaptureId = request.PaymentId;
        return Task.FromResult(Capture);
    }

    public Task<StepOutcome> VoidAsync(PaymentReferenceRequest request, CancellationToken cancellationToken)
    {
        LastVoidId = request.PaymentId;
        return Task.FromResult(Void);
    }

    public Task<StepOutcome> RefundAsync(PaymentReferenceRequest request, CancellationToken cancellationToken)
    {
        LastRefundId = request.PaymentId;
        return Task.FromResult(Refund);
    }

    public Task<StepOutcome> QueryByKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        LastQueryKey = idempotencyKey;
        return Task.FromResult(Query);
    }
}

internal sealed class MutableFakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

