using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Api.Tests.Persistence;

public sealed class LedgerPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly MutableTenantContextAccessor _tenant = new();

    public LedgerPersistenceTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Posted_earn_round_trips_with_both_legs()
    {
        var partner = PartnerId.New();
        _tenant.Set(TenantContext.Anonymous(partner));
        var member = LedgerAccount.MemberCredits(partner, MemberId.New());
        var issuance = LedgerAccount.Issuance(partner);
        var earn = LedgerTransaction.Earn(
            member,
            issuance,
            500,
            "earn-1",
            "Opening grant",
            new FixedClock(AsOf));

        await using (var write = CreateContext())
        {
            var repo = new LedgerRepository(write);
            await repo.AddAccountAsync(member, CancellationToken.None);
            await repo.AddAccountAsync(issuance, CancellationToken.None);
            await repo.AddAsync(earn, CancellationToken.None);
            await write.SaveChangesAsync();
        }

        await using var read = CreateContext();
        var loaded = await new LedgerRepository(read).GetByIdAsync(earn.Id, CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.Type.Should().Be(LedgerTransactionType.Earn);
        loaded.Entries.Should().HaveCount(2);
        loaded.Entries.Sum(e => e.Amount).Should().Be(0);
        loaded.Entries.Should().Contain(e => e.AccountId == member.Id && e.Amount == 500);
        loaded.Entries.Should().Contain(e => e.AccountId == issuance.Id && e.Amount == -500);
    }

    [Fact]
    public async Task Transactions_of_another_partner_are_invisible()
    {
        var summit = PartnerId.New();
        var nimbus = PartnerId.New();
        var summitMember = LedgerAccount.MemberCredits(summit, MemberId.New());
        var summitIssuance = LedgerAccount.Issuance(summit);
        var nimbusMember = LedgerAccount.MemberCredits(nimbus, MemberId.New());
        var nimbusIssuance = LedgerAccount.Issuance(nimbus);
        var clock = new FixedClock(AsOf);

        _tenant.Set(TenantContext.Anonymous(summit));
        await using (var write = CreateContext())
        {
            write.LedgerAccounts.AddRange(summitMember, summitIssuance);
            write.LedgerTransactions.Add(
                LedgerTransaction.Earn(summitMember, summitIssuance, 500, "s-1", "Summit grant", clock));
            await write.SaveChangesAsync();
        }

        _tenant.Set(TenantContext.Anonymous(nimbus));
        await using (var write = CreateContext())
        {
            write.LedgerAccounts.AddRange(nimbusMember, nimbusIssuance);
            write.LedgerTransactions.Add(
                LedgerTransaction.Earn(nimbusMember, nimbusIssuance, 120, "n-1", "Nimbus grant", clock));
            await write.SaveChangesAsync();
        }

        _tenant.Set(TenantContext.Anonymous(summit));
        await using var query = CreateContext();
        var visible = await new LedgerRepository(query).ListAsync(CancellationToken.None);

        visible.Should().ContainSingle();
        visible[0].IdempotencyKey.Should().Be("s-1");
        (await query.LedgerTransactions.IgnoreQueryFilters().CountAsync()).Should().Be(2);
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

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
