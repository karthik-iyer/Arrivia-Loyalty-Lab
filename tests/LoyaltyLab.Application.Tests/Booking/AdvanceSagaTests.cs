using LoyaltyLab.Application.Booking;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Application.Tests.Booking;

public sealed class AdvanceSagaTests
{
    [Fact]
    public async Task Happy_path_confirms_the_booking()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);

        var status = await world.Orchestrator().ExecuteAsync(world.Context, CancellationToken.None);

        status.Should().Be(SagaStatus.Confirmed);
        world.Saga.Steps.Should().OnlyContain(step => step.Status == SagaStepStatus.Succeeded);
        world.Bookings.Items.Should().ContainSingle(booking => booking.Status == BookingStatus.Confirmed);
        world.Outbox.Messages.Select(message => message.Type).Should().Equal(
            OutboxMessageTypes.CreditsBurned,
            OutboxMessageTypes.BookingConfirmed);
    }

    [Fact]
    public async Task Supplier_decline_compensates_with_nothing_to_undo()
    {
        var world = Harness.Create();
        world.Supplier.Reserve = StepOutcome.Failed(Errors.SupplierUnavailable);
        var log = new List<SagaStepKind>();

        var status = await Orchestrator(world, log).ExecuteAsync(world.Context, CancellationToken.None);

        status.Should().Be(SagaStatus.Compensated);
        world.Saga.Step(SagaStepKind.ReserveInventory).Status.Should().Be(SagaStepStatus.Failed);
        world.Saga.Step(SagaStepKind.ValidateQuote).Status.Should().Be(SagaStepStatus.Compensated);
        log.Should().Equal(SagaStepKind.ValidateQuote);
        world.Payments.LastVoidId.Should().BeNull();
        world.Supplier.LastReleased.Should().BeNull();
        world.Outbox.Messages.Should().ContainSingle(message => message.Type == OutboxMessageTypes.BookingCompensated);
    }

    [Fact]
    public async Task Payment_decline_compensates_the_reservation()
    {
        var world = Harness.Create();
        world.Payments.Authorize = StepOutcome.Failed(Errors.PaymentDeclined);
        var log = new List<SagaStepKind>();

        var status = await Orchestrator(world, log).ExecuteAsync(world.Context, CancellationToken.None);

        status.Should().Be(SagaStatus.Compensated);
        log.Should().Equal(SagaStepKind.ReserveInventory, SagaStepKind.ValidateQuote);
        world.Supplier.LastReleased.Should().Be("res-1");
    }

    [Fact]
    public async Task Insufficient_credits_compensates_payment_then_reservation()
    {
        var world = Harness.Create();
        await world.GrantAsync(10);
        var log = new List<SagaStepKind>();

        var status = await Orchestrator(world, log).ExecuteAsync(world.Context, CancellationToken.None);

        status.Should().Be(SagaStatus.Compensated);
        log.Should().Equal(
            SagaStepKind.AuthorizePayment,
            SagaStepKind.ReserveInventory,
            SagaStepKind.ValidateQuote);
        world.Payments.LastVoidId.Should().Be("pay-1");
        world.Supplier.LastReleased.Should().Be("res-1");
    }

    [Fact]
    public async Task Capture_failure_compensates_burn_then_payment_then_reservation()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        world.Payments.Capture = StepOutcome.Failed(Errors.PaymentDeclined);
        var log = new List<SagaStepKind>();

        var status = await Orchestrator(world, log).ExecuteAsync(world.Context, CancellationToken.None);

        status.Should().Be(SagaStatus.Compensated);
        log.Should().Equal(
            SagaStepKind.BurnCredits,
            SagaStepKind.AuthorizePayment,
            SagaStepKind.ReserveInventory,
            SagaStepKind.ValidateQuote);
        world.Ledger.Transactions.Should().Contain(t => t.Type == LedgerTransactionType.Reversal);
        world.Payments.LastVoidId.Should().Be("pay-1");
        world.Supplier.LastReleased.Should().Be("res-1");
    }

    [Fact]
    public async Task Supplier_timeout_parks_unknown_then_proceeds_when_query_succeeds()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        world.Supplier.Reserve = StepOutcome.Unknown();
        world.Supplier.Query = StepOutcome.Succeeded("res-1");

        var first = await world.Orchestrator().ExecuteAsync(world.Context, CancellationToken.None);

        first.Should().Be(SagaStatus.Running);
        world.Saga.Step(SagaStepKind.ReserveInventory).Status.Should().Be(SagaStepStatus.Unknown);

        var second = await world.Orchestrator().ExecuteAsync(world.Context, CancellationToken.None);

        second.Should().Be(SagaStatus.Confirmed);
        world.Saga.Step(SagaStepKind.ReserveInventory).Status.Should().Be(SagaStepStatus.Succeeded);
        world.Saga.Step(SagaStepKind.ReserveInventory).ExternalReference.Should().Be("res-1");
    }

    [Fact]
    public async Task Supplier_timeout_compensates_when_query_says_the_hold_never_landed()
    {
        var world = Harness.Create();
        world.Supplier.Reserve = StepOutcome.Unknown();
        world.Supplier.Query = StepOutcome.Failed(Errors.SupplierUnavailable);
        var log = new List<SagaStepKind>();
        var orchestrator = Orchestrator(world, log);

        await orchestrator.ExecuteAsync(world.Context, CancellationToken.None);
        var status = await orchestrator.ExecuteAsync(world.Context, CancellationToken.None);

        status.Should().Be(SagaStatus.Compensated);
        log.Should().Equal(SagaStepKind.ValidateQuote);
    }

    [Fact]
    public async Task Crash_after_persist_resumes_by_querying_not_re_executing()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        world.Saga.MarkInProgress(SagaStepKind.ValidateQuote, world.Clock);
        world.Saga.MarkSucceeded(SagaStepKind.ValidateQuote, null, world.Clock);
        world.Saga.Advance(world.Clock);
        world.Saga.MarkInProgress(SagaStepKind.ReserveInventory, world.Clock);
        world.Supplier.Reserve = StepOutcome.Failed(Errors.SupplierUnavailable);
        world.Supplier.Query = StepOutcome.Succeeded("res-1");

        var status = await world.Orchestrator().ExecuteAsync(world.Context, CancellationToken.None);

        status.Should().Be(SagaStatus.Confirmed);
        world.Supplier.Reserve.Result.Should().Be(StepResult.Failed);
        world.Saga.Step(SagaStepKind.ReserveInventory).ExternalReference.Should().Be("res-1");
    }

    [Fact]
    public async Task Exhausted_compensation_requires_manual_review()
    {
        var world = Harness.Create();
        world.Payments.Authorize = StepOutcome.Failed(Errors.PaymentDeclined);
        world.Supplier.Release = StepOutcome.Failed(Errors.SupplierUnavailable);

        var status = await world.Orchestrator().ExecuteAsync(world.Context, CancellationToken.None);

        status.Should().Be(SagaStatus.RequiresManualReview);
        world.Saga.Step(SagaStepKind.ReserveInventory).Status.Should().Be(SagaStepStatus.CompensationFailed);
        world.Saga.Step(SagaStepKind.ValidateQuote).Status.Should().Be(SagaStepStatus.Succeeded);
        world.Outbox.Messages.Should()
            .ContainSingle(message => message.Type == OutboxMessageTypes.BookingRequiresManualReview);
    }

    [Fact]
    public async Task In_progress_is_persisted_before_the_supplier_call()
    {
        var world = Harness.Create();
        var savesBeforeReserve = -1;
        world.Supplier.OnReserve = () =>
        {
            savesBeforeReserve = world.UnitOfWork.Saves;
            world.Saga.Step(SagaStepKind.ReserveInventory).Status.Should().Be(SagaStepStatus.InProgress);
        };
        world.Supplier.Reserve = StepOutcome.Failed(Errors.SupplierUnavailable);

        await world.Orchestrator().ExecuteAsync(world.Context, CancellationToken.None);

        savesBeforeReserve.Should().BeGreaterThan(0);
        world.Saga.Step(SagaStepKind.ReserveInventory).Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Transient_execute_failure_retries_then_confirms()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        var delay = new RecordingSagaDelay();
        world.Supplier.ReserveOnCall = call => call < 3
            ? StepOutcome.Failed(Errors.TemporaryFailure)
            : StepOutcome.Succeeded("res-1");

        var status = await Orchestrator(world, [], delay).ExecuteAsync(world.Context, CancellationToken.None);

        status.Should().Be(SagaStatus.Confirmed);
        world.Supplier.ReserveCalls.Should().Be(3);
        delay.Attempts.Should().Equal(1, 2);
        world.Saga.Step(SagaStepKind.ReserveInventory).Attempts.Should().Be(3);
    }

    [Fact]
    public async Task Permanent_execute_failure_does_not_retry()
    {
        var world = Harness.Create();
        world.Supplier.Reserve = StepOutcome.Failed(Errors.SupplierUnavailable);
        var delay = new RecordingSagaDelay();

        var status = await Orchestrator(world, [], delay).ExecuteAsync(world.Context, CancellationToken.None);

        status.Should().Be(SagaStatus.Compensated);
        world.Supplier.ReserveCalls.Should().Be(1);
        delay.Attempts.Should().BeEmpty();
    }

    [Fact]
    public async Task Compensation_retries_with_backoff_then_succeeds()
    {
        var world = Harness.Create();
        world.Payments.Authorize = StepOutcome.Failed(Errors.PaymentDeclined);
        world.Supplier.ReleaseOnCall = call => call < 3
            ? StepOutcome.Failed(Errors.SupplierUnavailable)
            : StepOutcome.Succeeded("res-1");
        var delay = new RecordingSagaDelay();

        var status = await Orchestrator(world, [], delay).ExecuteAsync(world.Context, CancellationToken.None);

        status.Should().Be(SagaStatus.Compensated);
        world.Supplier.ReleaseCalls.Should().Be(3);
        world.Supplier.LastReleased.Should().Be("res-1");
        delay.Attempts.Should().Equal(1, 2);
    }

    private static AdvanceSaga Orchestrator(
        Harness world,
        List<SagaStepKind> compensationLog,
        ISagaDelay? delay = null) =>
        new(
            [.. world.Steps().Select(step => new TracingStep(step, compensationLog))],
            world.UnitOfWork,
            world.Clock,
            delay ?? ImmediateSagaDelay.Instance,
            world.Outbox);

    private sealed class RecordingSagaDelay : ISagaDelay
    {
        public List<int> Attempts { get; } = [];

        public Task DelayAsync(int attempt, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            Attempts.Add(attempt);
            return Task.CompletedTask;
        }
    }

    private sealed class TracingStep(ISagaStep inner, List<SagaStepKind> compensationLog) : ISagaStep
    {
        public SagaStepKind Kind => inner.Kind;

        public int Order => inner.Order;

        public Task<StepOutcome> ExecuteAsync(SagaContext context, CancellationToken cancellationToken) =>
            inner.ExecuteAsync(context, cancellationToken);

        public Task<CompensationOutcome> CompensateAsync(SagaContext context, CancellationToken cancellationToken)
        {
            compensationLog.Add(inner.Kind);
            return inner.CompensateAsync(context, cancellationToken);
        }

        public Task<StepOutcome> ResolveUnknownAsync(SagaContext context, CancellationToken cancellationToken) =>
            inner.ResolveUnknownAsync(context, cancellationToken);
    }
}
