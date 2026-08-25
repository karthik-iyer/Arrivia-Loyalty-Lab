using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Api.Tests.Persistence;

public sealed class DemoSeedTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly MutableTenantContextAccessor _tenant = new();

    public DemoSeedTests()
    {
        _connection.Open();
        _tenant.Set(TenantContext.Anonymous(SeedIds.Summit));
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task EnsureAsync_is_idempotent()
    {
        await using (var first = CreateContext())
        {
            await DemoSeed.EnsureAsync(first);
            await DemoSeed.EnsureAsync(first);
        }

        await using var db = CreateContext();
        await DemoSeed.EnsureAsync(db);

        (await db.Partners.CountAsync()).Should().Be(2);
        (await db.Suppliers.CountAsync()).Should().Be(3);
        (await db.Offers.CountAsync()).Should().Be(24);
        (await db.Members.IgnoreQueryFilters().CountAsync()).Should().Be(3);
        (await db.PartnerSuppliers.IgnoreQueryFilters().CountAsync()).Should().Be(5);
        (await db.PricingRules.IgnoreQueryFilters().CountAsync()).Should().Be(8);
        (await db.LedgerTransactions.IgnoreQueryFilters().CountAsync()).Should().Be(3);
        (await db.BusyPeriods.IgnoreQueryFilters().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Seed_matches_the_two_partner_demo()
    {
        await using var db = CreateContext();
        await DemoSeed.EnsureAsync(db);

        var summit = await db.Partners.SingleAsync(p => p.Id == SeedIds.Summit);
        var nimbus = await db.Partners.SingleAsync(p => p.Id == SeedIds.Nimbus);

        summit.Code.Should().Be("SUMMIT");
        summit.CreditPolicy.DefaultBurnCap.Value.Should().Be(40m);
        summit.QuotePolicy.DriftPolicy.Should().Be(RateDriftPolicy.AbsorbWithinTolerance);

        nimbus.Code.Should().Be("NIMBUS");
        nimbus.CreditPolicy.DefaultBurnCap.Value.Should().Be(100m);
        nimbus.QuotePolicy.DriftPolicy.Should().Be(RateDriftPolicy.RequoteRequired);

        var members = await db.Members.IgnoreQueryFilters().ToListAsync();
        members.Should().Contain(m => m.Id == SeedIds.Maya && m.Tier == TierCode.Gold);
        members.Should().Contain(m => m.Id == SeedIds.Ravi && m.Tier == TierCode.Standard);
        members.Should().Contain(m => m.Id == SeedIds.Chen && m.PartnerId == SeedIds.Nimbus);

        var nimbusSuppliers = await db.PartnerSuppliers
            .IgnoreQueryFilters()
            .Where(p => p.PartnerId == SeedIds.Nimbus)
            .Select(p => p.SupplierId)
            .ToListAsync();

        nimbusSuppliers.Should().BeEquivalentTo([SeedIds.Alpine, SeedIds.Metro]);
        nimbusSuppliers.Should().NotContain(SeedIds.Oceanic);

        var summitRules = await db.PricingRules.IgnoreQueryFilters()
            .Where(r => r.PartnerId == SeedIds.Summit)
            .ToListAsync();
        summitRules.Should().HaveCount(5);
        summitRules.OfType<LoyaltyLab.Domain.Pricing.CampaignDiscountRule>()
            .Should().ContainSingle(r => r.CampaignCode == "MARCH-BEACH");

        var history = await db.LedgerTransactions.IgnoreQueryFilters().ToListAsync();
        var maya = await db.LedgerAccounts.IgnoreQueryFilters()
            .SingleAsync(account => account.MemberId == SeedIds.Maya);
        LedgerBalances.For(maya.Id, history).Should().Be(6_000);
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
