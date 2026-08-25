using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Persistence.Outbox;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoyaltyLab.Api.Tests.Persistence;

public sealed class OutboxDispatcherTests : IDisposable
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly string _sharedConnection =
        $"Data Source=outbox-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keeper;
    private readonly MutableTenantContextAccessor _tenant = new();
    private readonly Clock _clock = new(AsOf);

    public OutboxDispatcherTests()
    {
        _keeper = new SqliteConnection(_sharedConnection);
        _keeper.Open();
        using var bootstrap = CreateContext();
        bootstrap.Database.EnsureCreated();
    }

    public void Dispose() => _keeper.Dispose();

    [Fact]
    public async Task Killing_the_process_after_commit_still_delivers_the_event()
    {
        var partner = PartnerId.New();
        _tenant.Set(TenantContext.Anonymous(partner));
        var member = Member.Create(partner, "Maya", TierCode.Gold);
        var message = OutboxMessage.Create(
            partner,
            OutboxMessageTypes.BookingConfirmed,
            """{"sagaId":"s1"}""",
            "corr-1",
            _clock);

        await using (var write = CreateContext())
        {
            write.Members.Add(member);
            new EfOutbox(write).Enqueue(message);
            await write.SaveChangesAsync();
        }

        var handler = new RecordingHandler(OutboxMessageTypes.BookingConfirmed);
        await using (var dispatch = CreateContext())
        {
            await Dispatcher(dispatch, handler, maxAttempts: 5).DispatchAsync(CancellationToken.None);
        }

        handler.Delivered.Should().ContainSingle(delivered => delivered.Id == message.Id);
        await using var read = CreateContext();
        var stored = await read.OutboxMessages.SingleAsync();
        stored.DispatchedAt.Should().Be(AsOf);
        (await read.Members.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Transient_handler_failure_retries_then_dispatches()
    {
        var partner = PartnerId.New();
        _tenant.Set(TenantContext.Anonymous(partner));
        var message = Seed(partner, OutboxMessageTypes.CreditsBurned, "corr-retry");
        var handler = new RecordingHandler(OutboxMessageTypes.CreditsBurned) { FailUntilAttempt = 2 };

        await using var db = CreateContext();
        var dispatcher = Dispatcher(db, handler, maxAttempts: 3);
        await dispatcher.DispatchAsync(CancellationToken.None);
        await dispatcher.DispatchAsync(CancellationToken.None);
        await dispatcher.DispatchAsync(CancellationToken.None);

        handler.Calls.Should().Be(3);
        handler.Delivered.Should().ContainSingle();
        var stored = await db.OutboxMessages.IgnoreQueryFilters().SingleAsync();
        stored.Id.Should().Be(message.Id);
        stored.DispatchedAt.Should().Be(AsOf);
        stored.Attempts.Should().Be(2);
        (await db.PoisonMessages.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Exhausted_retries_move_to_poison_and_do_not_block_later_messages()
    {
        var partner = PartnerId.New();
        _tenant.Set(TenantContext.Anonymous(partner));
        var first = OutboxMessage.Create(
            partner,
            OutboxMessageTypes.BookingConfirmed,
            """{"order":1}""",
            "corr-poison",
            _clock,
            id: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var second = OutboxMessage.Create(
            partner,
            OutboxMessageTypes.BookingConfirmed,
            """{"order":2}""",
            "corr-poison",
            new Clock(AsOf.AddSeconds(1)),
            id: Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"));

        await using (var write = CreateContext())
        {
            new EfOutbox(write).Enqueue(first);
            new EfOutbox(write).Enqueue(second);
            await write.SaveChangesAsync();
        }

        var handler = new RecordingHandler(OutboxMessageTypes.BookingConfirmed)
        {
            FailForId = first.Id,
        };

        await using var db = CreateContext();
        var dispatcher = Dispatcher(db, handler, maxAttempts: 2);
        await dispatcher.DispatchAsync(CancellationToken.None);
        await dispatcher.DispatchAsync(CancellationToken.None);

        (await db.OutboxMessages.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        var remaining = await db.OutboxMessages.IgnoreQueryFilters().SingleAsync();
        remaining.Id.Should().Be(second.Id);
        remaining.DispatchedAt.Should().Be(AsOf);
        var poisoned = await db.PoisonMessages.IgnoreQueryFilters().SingleAsync();
        poisoned.OutboxMessageId.Should().Be(first.Id);
        poisoned.CorrelationId.Should().Be("corr-poison");
        poisoned.Attempts.Should().Be(2);
        handler.Delivered.Should().ContainSingle(delivered => delivered.Id == second.Id);
    }

    [Fact]
    public async Task Handler_is_idempotent_on_message_id()
    {
        var partner = PartnerId.New();
        _tenant.Set(TenantContext.Anonymous(partner));
        var message = Seed(partner, OutboxMessageTypes.BookingCompensated, "corr-idemp");
        var handler = new IdempotentHandler(OutboxMessageTypes.BookingCompensated);

        await using (var first = CreateContext())
        {
            await Dispatcher(first, handler, maxAttempts: 5).DispatchAsync(CancellationToken.None);
        }

        await using (var reset = CreateContext())
        {
            var stored = await reset.OutboxMessages.IgnoreQueryFilters().SingleAsync();
            reset.Entry(stored).Property(m => m.DispatchedAt).CurrentValue = null;
            await reset.SaveChangesAsync();
        }

        await using (var second = CreateContext())
        {
            await Dispatcher(second, handler, maxAttempts: 5).DispatchAsync(CancellationToken.None);
        }

        handler.Calls.Should().Be(2);
        handler.Applied.Should().Be(1);
        handler.Seen.Should().Equal(message.Id);
    }

    [Fact]
    public async Task Outbox_rows_of_another_partner_are_invisible()
    {
        var summit = PartnerId.New();
        var nimbus = PartnerId.New();
        _tenant.Set(TenantContext.Anonymous(summit));
        Seed(summit, OutboxMessageTypes.BookingConfirmed, "corr-a");

        _tenant.Set(TenantContext.Anonymous(nimbus));
        await using var query = CreateContext();
        (await query.OutboxMessages.CountAsync()).Should().Be(0);
        (await query.OutboxMessages.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    private OutboxMessage Seed(PartnerId partner, string type, string correlationId)
    {
        var message = OutboxMessage.Create(partner, type, "{}", correlationId, _clock);
        using var write = CreateContext();
        new EfOutbox(write).Enqueue(message);
        write.SaveChanges();
        return message;
    }

    private OutboxDispatcher Dispatcher(LoyaltyLabDbContext db, IOutboxHandler handler, int maxAttempts) =>
        new(
            db,
            _tenant,
            _clock,
            [handler],
            Options.Create(new OutboxOptions { MaxAttempts = maxAttempts }));

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

    private sealed class Clock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class RecordingHandler(string messageType) : IOutboxHandler
    {
        public string MessageType { get; } = messageType;

        public int FailUntilAttempt { get; init; }

        public Guid? FailForId { get; init; }

        public int Calls { get; private set; }

        public List<OutboxMessage> Delivered { get; } = [];

        public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            Calls++;
            if (FailForId == message.Id || Calls <= FailUntilAttempt)
            {
                throw new InvalidOperationException("injected handler failure");
            }

            Delivered.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class IdempotentHandler(string messageType) : IOutboxHandler
    {
        private readonly HashSet<Guid> _seen = [];

        public string MessageType { get; } = messageType;

        public int Calls { get; private set; }

        public int Applied { get; private set; }

        public IReadOnlyCollection<Guid> Seen => _seen;

        public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            Calls++;
            if (_seen.Add(message.Id))
            {
                Applied++;
            }

            return Task.CompletedTask;
        }
    }
}
