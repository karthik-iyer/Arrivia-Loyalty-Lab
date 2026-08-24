using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Tenancy;
using LoyaltyLab.Infrastructure.Time;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Api.Tests.Persistence;

public sealed class QuotePersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly MutableTenantContextAccessor _tenant = new();

    public QuotePersistenceTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Quote_round_trips_trace_including_the_clamp()
    {
        var partner = Partner.Create(
            "SUMMIT",
            "Summit Rewards",
            Currency.Usd,
            new PartnerTheme("#BE185D", "#FFF7ED", "#1D4ED8"),
            new CreditPolicy(0.01m, Percent.From(40m), 730, Percent.From(10m)),
            new QuotePolicy(15, RateDriftPolicy.AbsorbWithinTolerance, Percent.From(2m)),
            new SagaPolicy(10, 3, 5, 60),
            new OpportunityPolicy(3, 14, 0.55m, Percent.From(10m), 2, 30, 7, new SignalWeights(0.2m, 0.2m, 0.2m, 0.2m, 0.2m)));
        var member = Member.Create(partner.Id, "Maya", TierCode.Gold);
        var offer = TravelOffer.Create(
            SupplierId.New(),
            "Coral Bay Resort",
            new Destination("MBJ", "Montego Bay"),
            Money.Of(100.00m, Currency.Usd),
            Money.Of(15.00m, Currency.Usd),
            [OfferTag.Beach],
            4,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));
        var asOf = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var rules = new PricingRule[]
        {
            BaseMarkupRule.Create(partner.Id, Percent.From(12m), RuleScope.PartnerWide, asOf),
            TierAdjustmentRule.Create(partner.Id, Percent.From(-3m), new RuleScope(tier: TierCode.Gold), asOf),
            CampaignDiscountRule.Create(partner.Id, "MARCH-BEACH", Percent.From(-5m), new RuleScope(tag: OfferTag.Beach), asOf),
            MarginFloorRule.Create(partner.Id, Percent.From(5m), RuleScope.PartnerWide, asOf),
            BurnCapRule.Create(partner.Id, Percent.From(40m), RuleScope.PartnerWide, asOf),
        };
        var request = new PricingRequest(
            PricingContext.ForOffer(partner.Id, offer, TierCode.Gold, new DateOnly(2026, 3, 15)),
            offer,
            new HashSet<SupplierId> { offer.SupplierId },
            rules,
            asOf);
        var state = new PricingPipeline().Execute(request);
        var quote = Quote.Create(member, offer, state, partner.QuotePolicy, new FixedDemoClock(asOf));

        _tenant.Set(TenantContext.Anonymous(partner.Id));

        await using (var db = CreateContext())
        {
            db.Quotes.Add(quote);
            await db.SaveChangesAsync();
        }

        await using var query = CreateContext();
        var loaded = await query.Quotes.SingleAsync(q => q.Id == quote.Id);

        loaded.MemberPrice.Amount.Should().Be(120.75m);
        loaded.Trace.Should().Contain(e => e.WasClamped);
        loaded.NetRateSnapshot.Amount.Should().Be(100.00m);
    }

    private LoyaltyLabDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LoyaltyLabDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new LoyaltyLabDbContext(options, _tenant);
        context.Database.EnsureCreated();
        return context;
    }
}
