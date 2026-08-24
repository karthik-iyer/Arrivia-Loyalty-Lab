using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Idempotency;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Tests;

public sealed class IdempotencyUseCaseTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task First_claim_reserves_the_key()
    {
        var world = World.Create();

        var result = await world.Claim.ExecuteAsync(
            new ClaimIdempotencyCommand("Earn", "grant-1", """{"credits":500}"""),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsReplay.Should().BeFalse();
        result.Value.Record.PayloadHash.Should().Be(IdempotencyHash.Compute("""{"credits":500}"""));
    }

    [Fact]
    public async Task Same_key_and_payload_is_a_replay()
    {
        var world = World.Create();
        var command = new ClaimIdempotencyCommand("Earn", "grant-1", """{"credits":500}""");

        await world.Claim.ExecuteAsync(command, CancellationToken.None);
        var replay = await world.Claim.ExecuteAsync(command, CancellationToken.None);

        replay.IsSuccess.Should().BeTrue();
        replay.Value.IsReplay.Should().BeTrue();
    }

    [Fact]
    public async Task Same_key_and_different_payload_is_reused()
    {
        var world = World.Create();

        await world.Claim.ExecuteAsync(
            new ClaimIdempotencyCommand("Earn", "grant-1", """{"credits":500}"""),
            CancellationToken.None);

        var reused = await world.Claim.ExecuteAsync(
            new ClaimIdempotencyCommand("Earn", "grant-1", """{"credits":200}"""),
            CancellationToken.None);

        reused.IsFailure.Should().BeTrue();
        reused.Error.Should().Be(Errors.IdempotencyKeyReused);
    }

    private sealed class World(ClaimIdempotency claim)
    {
        public ClaimIdempotency Claim { get; } = claim;

        public static World Create()
        {
            var tenant = new FakeTenant
            {
                Current = TenantContext.Anonymous(PartnerId.New()),
            };
            return new World(new ClaimIdempotency(tenant, new FakeIdempotencyStore(), new FakeClock(AsOf)));
        }
    }
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
