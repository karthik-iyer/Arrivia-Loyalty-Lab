using LoyaltyLab.Application.Booking;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Tests.Booking;

public sealed class StartBookingSagaTests
{
    private static readonly DateOnly Stay = new(2026, 6, 1);

    [Fact]
    public async Task Start_confirms_and_exposes_every_step()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        var start = await UseCase(world);

        var result = await start.ExecuteAsync(Command(world, 4830, "start-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BookingStatus.Confirmed);
        result.Value.Saga.Status.Should().Be(SagaStatus.Confirmed);
        result.Value.Saga.Steps.Should().HaveCount(6);
        result.Value.Saga.Steps.Should().OnlyContain(step => step.Status == SagaStepStatus.Succeeded);
        result.Value.Tender.CreditsApplied.Should().Be(4830);
    }

    [Fact]
    public async Task Same_key_replays_the_confirmed_booking()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        var start = await UseCase(world);

        var first = await start.ExecuteAsync(Command(world, 4830, "start-replay"), CancellationToken.None);
        var replay = await start.ExecuteAsync(Command(world, 4830, "start-replay"), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        replay.IsSuccess.Should().BeTrue();
        replay.Value.BookingId.Should().Be(first.Value.BookingId);
        world.Bookings.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Credit_tender_above_balance_is_insufficient()
    {
        var world = Harness.Create();
        var start = await UseCase(world);

        var result = await start.ExecuteAsync(Command(world, 4830, "start-poor"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.InsufficientCredits);
    }

    private static async Task<StartBookingSaga> UseCase(Harness world)
    {
        var tenant = new FakeTenant { Current = TenantContext.ForMember(world.Context.Member) };
        var quotes = new FakeQuotes(tenant);
        await quotes.AddAsync(world.Quote, CancellationToken.None);
        var claim = new ClaimIdempotency(tenant, new FakeIdempotencyStore(), world.Clock);
        return new StartBookingSaga(
            tenant,
            quotes,
            new FakeOffers(world.Context.Offer),
            new FakeMembers(world.Context.Member),
            new FakePartners(world.Context.Partner),
            new FakeRules(),
            world.Bookings,
            new FakeSagas(),
            claim,
            new GetBalance(
                tenant,
                new FakeMembers(world.Context.Member),
                new FakePartners(world.Context.Partner),
                world.Ledger),
            world.Orchestrator(),
            world.UnitOfWork,
            world.Clock);
    }

    private static StartBookingSagaCommand Command(Harness world, int credits, string key) =>
        new(world.Quote.Id, credits, Stay, key, "corr-start");
}

public sealed class CancelBookingTests
{
    [Fact]
    public async Task Cancel_restores_the_granted_balance()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        var tenant = new FakeTenant { Current = TenantContext.ForMember(world.Context.Member) };
        var quotes = new FakeQuotes(tenant);
        await quotes.AddAsync(world.Quote, CancellationToken.None);
        var sagas = new FakeSagas();
        var store = new FakeIdempotencyStore();
        var start = new StartBookingSaga(
            tenant,
            quotes,
            new FakeOffers(world.Context.Offer),
            new FakeMembers(world.Context.Member),
            new FakePartners(world.Context.Partner),
            new FakeRules(),
            world.Bookings,
            sagas,
            new ClaimIdempotency(tenant, store, world.Clock),
            new GetBalance(
                tenant,
                new FakeMembers(world.Context.Member),
                new FakePartners(world.Context.Partner),
                world.Ledger),
            world.Orchestrator(),
            world.UnitOfWork,
            world.Clock);

        var booked = await start.ExecuteAsync(
            new StartBookingSagaCommand(world.Quote.Id, 4830, new DateOnly(2026, 6, 1), "start-cancel", "corr"),
            CancellationToken.None);
        booked.IsSuccess.Should().BeTrue();

        var cancel = new CancelBooking(
            tenant,
            world.Bookings,
            sagas,
            new ClaimIdempotency(tenant, store, world.Clock),
            world.Reverse,
            world.Payments,
            world.Supplier,
            world.UnitOfWork);

        var cancelled = await cancel.ExecuteAsync(
            new CancelBookingCommand(booked.Value.BookingId, "cancel-1"),
            CancellationToken.None);
        var balance = await new GetBalance(
            tenant,
            new FakeMembers(world.Context.Member),
            new FakePartners(world.Context.Partner),
            world.Ledger).ExecuteAsync(new GetBalanceQuery(), CancellationToken.None);

        cancelled.IsSuccess.Should().BeTrue();
        cancelled.Value.Status.Should().Be(BookingStatus.Cancelled);
        balance.Value.Credits.Should().Be(5_000);
        world.Payments.LastRefundId.Should().Be("pay-1");
        world.Supplier.LastReleased.Should().Be("res-1");
    }
}

public sealed class GetSagaInstanceTests
{
    [Fact]
    public async Task Operator_view_includes_compensation_on_unwound_steps()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        world.Payments.Authorize = StepOutcome.Failed(Errors.PaymentDeclined);
        var tenant = new FakeTenant { Current = TenantContext.ForMember(world.Context.Member) };
        var quotes = new FakeQuotes(tenant);
        await quotes.AddAsync(world.Quote, CancellationToken.None);
        var sagas = new FakeSagas();
        var start = new StartBookingSaga(
            tenant,
            quotes,
            new FakeOffers(world.Context.Offer),
            new FakeMembers(world.Context.Member),
            new FakePartners(world.Context.Partner),
            new FakeRules(),
            world.Bookings,
            sagas,
            new ClaimIdempotency(tenant, new FakeIdempotencyStore(), world.Clock),
            new GetBalance(
                tenant,
                new FakeMembers(world.Context.Member),
                new FakePartners(world.Context.Partner),
                world.Ledger),
            world.Orchestrator(),
            world.UnitOfWork,
            world.Clock);

        var booked = await start.ExecuteAsync(
            new StartBookingSagaCommand(world.Quote.Id, 4830, new DateOnly(2026, 6, 1), "start-decline", "corr-decline"),
            CancellationToken.None);
        booked.IsSuccess.Should().BeTrue();
        booked.Value.Saga.Status.Should().Be(SagaStatus.Compensated);

        tenant.Current = TenantContext.ForRole(world.Context.Partner.Id, AccessRole.Operator);
        var detail = await new GetSagaInstance(tenant, sagas, new FakePoison())
            .ExecuteAsync(new GetSagaInstanceQuery(booked.Value.Saga.Id), CancellationToken.None);

        detail.IsSuccess.Should().BeTrue();
        var reserved = detail.Value.Saga.Steps.Single(step => step.Kind == SagaStepKind.ReserveInventory);
        reserved.Status.Should().Be(SagaStepStatus.Compensated);
        reserved.Compensation.Should().NotBeNull();
        reserved.Compensation!.Status.Should().Be(CompensationStatus.Succeeded);
        reserved.Attempts.Should().BeGreaterThanOrEqualTo(1);
    }
}
