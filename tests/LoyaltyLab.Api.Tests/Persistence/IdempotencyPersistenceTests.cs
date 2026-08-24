using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Idempotency;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Api.Tests.Persistence;

public sealed class IdempotencyPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly string _sharedConnection =
        $"Data Source=idempotency-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keeper;
    private readonly MutableTenantContextAccessor _tenant = new();

    public IdempotencyPersistenceTests()
    {
        _keeper = new SqliteConnection(_sharedConnection);
        _keeper.Open();
        using var bootstrap = CreateContext();
        bootstrap.Database.EnsureCreated();
    }

    public void Dispose() => _keeper.Dispose();

    [Fact]
    public async Task Concurrent_same_key_inserts_once()
    {
        var partner = PartnerId.New();
        _tenant.Set(TenantContext.Anonymous(partner));
        var hash = IdempotencyHash.Compute("""{"credits":500}""");

        async Task<bool> TryInsertAsync()
        {
            await using var db = CreateContext();
            var record = new IdempotencyRecord(partner, "Earn", "grant-1", hash, AsOf);
            return await new IdempotencyStore(db).SaveAsync(record, CancellationToken.None);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => TryInsertAsync()));

        results.Count(won => won).Should().Be(1);

        await using var query = CreateContext();
        (await query.IdempotencyRecords.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_same_key_and_payload_has_one_first_effect()
    {
        var partner = PartnerId.New();
        _tenant.Set(TenantContext.Anonymous(partner));
        var clock = new FixedClock(AsOf);
        var command = new ClaimIdempotencyCommand("Earn", "grant-1", """{"credits":500}""");

        async Task<Result<IdempotencyClaim>> ClaimAsync()
        {
            await using var db = CreateContext();
            var useCase = new ClaimIdempotency(_tenant, new IdempotencyStore(db), clock);
            return await useCase.ExecuteAsync(command, CancellationToken.None);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ClaimAsync()));

        results.Should().OnlyContain(result => result.IsSuccess);
        results.Count(result => !result.Value.IsReplay).Should().Be(1);
        results.Count(result => result.Value.IsReplay).Should().Be(7);
    }

    [Fact]
    public async Task Different_payload_returns_idempotency_key_reused()
    {
        var partner = PartnerId.New();
        _tenant.Set(TenantContext.Anonymous(partner));
        var clock = new FixedClock(AsOf);

        await using (var first = CreateContext())
        {
            var claim = new ClaimIdempotency(_tenant, new IdempotencyStore(first), clock);
            (await claim.ExecuteAsync(
                    new ClaimIdempotencyCommand("Earn", "grant-1", """{"credits":500}"""),
                    CancellationToken.None))
                .IsSuccess.Should().BeTrue();
        }

        await using var second = CreateContext();
        var reused = await new ClaimIdempotency(_tenant, new IdempotencyStore(second), clock)
            .ExecuteAsync(
                new ClaimIdempotencyCommand("Earn", "grant-1", """{"credits":200}"""),
                CancellationToken.None);

        reused.IsFailure.Should().BeTrue();
        reused.Error.Should().Be(Errors.IdempotencyKeyReused);
    }

    [Fact]
    public async Task Records_of_another_partner_are_invisible()
    {
        var summit = PartnerId.New();
        var nimbus = PartnerId.New();
        var hash = IdempotencyHash.Compute("same");

        _tenant.Set(TenantContext.Anonymous(summit));
        await using (var write = CreateContext())
        {
            write.IdempotencyRecords.Add(new IdempotencyRecord(summit, "Earn", "k", hash, AsOf));
            await write.SaveChangesAsync();
        }

        _tenant.Set(TenantContext.Anonymous(nimbus));
        await using (var write = CreateContext())
        {
            write.IdempotencyRecords.Add(new IdempotencyRecord(nimbus, "Earn", "k", hash, AsOf));
            await write.SaveChangesAsync();
        }

        _tenant.Set(TenantContext.Anonymous(summit));
        await using var query = CreateContext();
        var visible = await query.IdempotencyRecords.ToListAsync();

        visible.Should().ContainSingle();
        visible[0].PartnerId.Should().Be(summit);
        (await query.IdempotencyRecords.IgnoreQueryFilters().CountAsync()).Should().Be(2);
    }

    private LoyaltyLabDbContext CreateContext()
    {
        if (!_tenant.HasCurrent)
        {
            _tenant.Set(TenantContext.Anonymous(PartnerId.New()));
        }

        var options = new DbContextOptionsBuilder<LoyaltyLabDbContext>()
            .UseSqlite(_sharedConnection)
            .Options;

        var context = new LoyaltyLabDbContext(options, _tenant);
        context.Database.ExecuteSqlRaw("PRAGMA busy_timeout = 5000;");
        return context;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
