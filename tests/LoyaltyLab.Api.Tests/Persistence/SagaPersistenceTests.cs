using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Api.Tests.Persistence;

public sealed class SagaPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly string _sharedConnection =
        $"Data Source=sagas-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keeper;
    private readonly MutableTenantContextAccessor _tenant = new();

    public SagaPersistenceTests()
    {
        _keeper = new SqliteConnection(_sharedConnection);
        _keeper.Open();
        using var bootstrap = CreateContext();
        bootstrap.Database.EnsureCreated();
    }

    public void Dispose() => _keeper.Dispose();

    [Fact]
    public async Task Two_sagas_for_one_booking_are_impossible()
    {
        var partner = PartnerId.New();
        var booking = BookingId.New();
        _tenant.Set(TenantContext.Anonymous(partner));

        await using (var first = CreateContext())
        {
            await new SagaRepository(first).AddAsync(Start(partner, booking), CancellationToken.None);
            await first.SaveChangesAsync();
        }

        await using var second = CreateContext();
        await new SagaRepository(second).AddAsync(Start(partner, booking), CancellationToken.None);
        var act = async () => await second.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().BeOfType<SqliteException>()
            .Which.SqliteErrorCode.Should().Be(19); // SQLITE_CONSTRAINT
    }

    [Fact]
    public async Task Concurrent_inserts_for_one_booking_leave_a_single_saga()
    {
        var partner = PartnerId.New();
        var booking = BookingId.New();
        _tenant.Set(TenantContext.Anonymous(partner));

        async Task<bool> TryAddAsync()
        {
            await using var db = CreateContext();
            await new SagaRepository(db).AddAsync(Start(partner, booking), CancellationToken.None);
            try
            {
                await db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => TryAddAsync()));

        results.Count(won => won).Should().Be(1);
        await using var query = CreateContext();
        (await query.SagaInstances.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Instance_round_trips_steps_and_derived_keys()
    {
        var partner = PartnerId.New();
        _tenant.Set(TenantContext.Anonymous(partner));
        var saga = Start(partner, BookingId.New());
        saga.MarkInProgress(SagaStepKind.ValidateQuote, new Clock(AsOf));

        await using (var write = CreateContext())
        {
            await new SagaRepository(write).AddAsync(saga, CancellationToken.None);
            await write.SaveChangesAsync();
        }

        await using var read = CreateContext();
        var loaded = await new SagaRepository(read).GetByIdAsync(saga.Id, CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.BookingId.Should().Be(saga.BookingId);
        loaded.Steps.Should().HaveCount(SagaInstance.StepCount);
        loaded.Step(SagaStepKind.ValidateQuote).Status.Should().Be(SagaStepStatus.InProgress);
        loaded.Step(SagaStepKind.ValidateQuote).IdempotencyKey
            .Should().Be(SagaInstance.DeriveIdempotencyKey(saga.Id, SagaStepKind.ValidateQuote));
        loaded.Version.Should().Be(1);
    }

    [Fact]
    public async Task Stale_version_cannot_overwrite_a_newer_write()
    {
        var partner = PartnerId.New();
        _tenant.Set(TenantContext.Anonymous(partner));
        var saga = Start(partner, BookingId.New());

        await using (var seed = CreateContext())
        {
            await new SagaRepository(seed).AddAsync(saga, CancellationToken.None);
            await seed.SaveChangesAsync();
        }

        await using var first = CreateContext();
        await using var second = CreateContext();
        var left = await new SagaRepository(first).GetByIdAsync(saga.Id, CancellationToken.None);
        var right = await new SagaRepository(second).GetByIdAsync(saga.Id, CancellationToken.None);

        left!.MarkInProgress(SagaStepKind.ValidateQuote, new Clock(AsOf));
        await first.SaveChangesAsync();

        right!.MarkInProgress(SagaStepKind.ValidateQuote, new Clock(AsOf));
        var act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Sagas_of_another_partner_are_invisible()
    {
        var summit = PartnerId.New();
        var nimbus = PartnerId.New();

        _tenant.Set(TenantContext.Anonymous(summit));
        await using (var write = CreateContext())
        {
            await new SagaRepository(write).AddAsync(Start(summit, BookingId.New()), CancellationToken.None);
            await write.SaveChangesAsync();
        }

        _tenant.Set(TenantContext.Anonymous(nimbus));
        await using var query = CreateContext();
        (await query.SagaInstances.CountAsync()).Should().Be(0);
        (await query.SagaInstances.IgnoreQueryFilters().CountAsync()).Should().Be(1);
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

    private static SagaInstance Start(PartnerId partnerId, BookingId bookingId) =>
        SagaInstance.Start(partnerId, bookingId, "corr-1", new Clock(AsOf));

    private sealed class Clock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
