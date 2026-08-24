using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
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
