using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Api.Tests.Persistence;

public sealed class TenantFilterTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly MutableTenantContextAccessor _tenant = new();

    public TenantFilterTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Members_of_another_partner_are_invisible()
    {
        var summit = CreatePartner("SUMMIT", "Summit Rewards");
        var nimbus = CreatePartner("NIMBUS", "Nimbus Club");

        await using (var seed = CreateContext())
        {
            seed.Partners.AddRange(summit, nimbus);
            seed.Members.AddRange(
                Member.Create(summit.Id, "Maya", TierCode.Gold),
                Member.Create(nimbus.Id, "Chen", TierCode.Standard));
            await seed.SaveChangesAsync();
        }

        _tenant.Set(TenantContext.Anonymous(summit.Id));

        await using var query = CreateContext();
        var visible = await query.Members.ToListAsync();

        visible.Should().ContainSingle(m => m.DisplayName == "Maya");
        visible.Should().NotContain(m => m.DisplayName == "Chen");
    }

    [Fact]
    public async Task IgnoreQueryFilters_is_the_only_way_to_see_cross_tenant_rows()
    {
        var summit = CreatePartner("SUMMIT", "Summit Rewards");
        var nimbus = CreatePartner("NIMBUS", "Nimbus Club");

        await using (var seed = CreateContext())
        {
            seed.Partners.AddRange(summit, nimbus);
            seed.Members.AddRange(
                Member.Create(summit.Id, "Maya", TierCode.Gold),
                Member.Create(nimbus.Id, "Chen", TierCode.Standard));
            await seed.SaveChangesAsync();
        }

        _tenant.Set(TenantContext.Anonymous(summit.Id));

        await using var query = CreateContext();
        (await query.Members.CountAsync()).Should().Be(1);
        (await query.Members.IgnoreQueryFilters().CountAsync()).Should().Be(2);
    }

    private LoyaltyLabDbContext CreateContext()
    {
        if (!_tenant.HasCurrent)
        {
            _tenant.Set(TenantContext.Anonymous(PartnerId.New()));
        }

        var options = new DbContextOptionsBuilder<LoyaltyLabDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new LoyaltyLabDbContext(options, _tenant);
        context.Database.EnsureCreated();
        return context;
    }

    private static Partner CreatePartner(string code, string name) =>
        Partner.Create(
            code,
            name,
            Currency.Usd,
            new PartnerTheme("#BE185D", "#FFF7ED", "#1D4ED8"),
            new CreditPolicy(0.01m, Percent.From(40m), 730, Percent.From(10m)),
            new QuotePolicy(15, RateDriftPolicy.AbsorbWithinTolerance, Percent.From(2m)),
            new SagaPolicy(10, 3, 5, 60),
            new OpportunityPolicy(3, 14, 0.55m, Percent.From(10m), 2, 30, 7, new SignalWeights(0.2m, 0.2m, 0.2m, 0.2m, 0.2m)));
}
