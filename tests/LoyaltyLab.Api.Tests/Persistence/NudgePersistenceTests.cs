using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Tenancy;
using LoyaltyLab.Infrastructure.Time;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Api.Tests.Persistence;

public sealed class NudgePersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly MutableTenantContextAccessor _tenant = new();

    public NudgePersistenceTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Delivered_nudge_round_trips_every_signal()
    {
        var partner = CreatePartner();
        var member = Member.Create(partner.Id, "Maya", TierCode.Gold);
        var window = new TravelWindow(member.Id, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var signals = new[]
        {
            OpportunitySignal.Of(SignalKind.WindowFit, 14m, 1m, 0.2m),
            OpportunitySignal.Of(SignalKind.CreditCoverage, 0.4m, 0.4m, 0.2m),
        };
        var clock = new FixedDemoClock(new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));
        var nudge = Nudge.Deliver(
            partner.Id,
            member.Id,
            OfferId.New(),
            window,
            signals,
            partner.OpportunityPolicy,
            clock);

        _tenant.Set(TenantContext.Anonymous(partner.Id));

        await using (var db = CreateContext())
        {
            db.Nudges.Add(nudge);
            await db.SaveChangesAsync();
        }

        await using var query = CreateContext();
        var loaded = await query.Nudges.SingleAsync(row => row.Id == nudge.Id);

        loaded.Status.Should().Be(NudgeStatus.Delivered);
        loaded.Score.Should().Be(0.28m);
        loaded.Signals.Should().HaveCount(2);
        loaded.Signals.Select(signal => signal.Kind).Should().Equal(SignalKind.WindowFit, SignalKind.CreditCoverage);
    }

    [Fact]
    public async Task Suppressed_nudge_is_persisted_with_its_reason()
    {
        var partner = CreatePartner();
        var member = Member.Create(partner.Id, "Maya", TierCode.Gold);
        var window = new TravelWindow(member.Id, new DateOnly(2026, 3, 29), new DateOnly(2026, 4, 12));
        var clock = new FixedDemoClock(new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));
        var nudge = Nudge.Suppress(
            partner.Id,
            member.Id,
            window,
            SuppressionReason.NoEligibleInventory,
            partner.OpportunityPolicy,
            clock);

        _tenant.Set(TenantContext.Anonymous(partner.Id));

        await using (var db = CreateContext())
        {
            db.Nudges.Add(nudge);
            await db.SaveChangesAsync();
        }

        await using var query = CreateContext();
        var loaded = await query.Nudges.SingleAsync(row => row.Id == nudge.Id);

        loaded.Status.Should().Be(NudgeStatus.Suppressed);
        loaded.SuppressedBecause.Should().Be(SuppressionReason.NoEligibleInventory);
        loaded.Signals.Should().BeEmpty();
    }

    [Fact]
    public async Task Busy_periods_are_tenant_filtered()
    {
        var summit = CreatePartner("SUMMIT", "Summit Rewards");
        var nimbus = CreatePartner("NIMBUS", "Nimbus Club");
        var maya = Member.Create(summit.Id, "Maya", TierCode.Gold);
        var chen = Member.Create(nimbus.Id, "Chen", TierCode.Standard);

        await using (var seed = CreateContext())
        {
            seed.BusyPeriods.AddRange(
                BusyPeriod.Create(summit.Id, maya.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 29)),
                BusyPeriod.Create(nimbus.Id, chen.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1)));
            await seed.SaveChangesAsync();
        }

        _tenant.Set(TenantContext.Anonymous(summit.Id));
        await using var query = CreateContext();
        var visible = await query.BusyPeriods.ToListAsync();

        visible.Should().ContainSingle(row => row.MemberId == maya.Id);
        visible.Should().NotContain(row => row.MemberId == chen.Id);
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

    private static Partner CreatePartner(string code = "SUMMIT", string name = "Summit Rewards") =>
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
