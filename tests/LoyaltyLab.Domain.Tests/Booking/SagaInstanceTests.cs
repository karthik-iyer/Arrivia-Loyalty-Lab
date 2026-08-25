using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Booking;

public sealed class SagaInstanceTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_seeds_six_pending_steps_with_derived_keys()
    {
        var id = SagaInstanceId.New();
        var saga = Start(id);

        saga.Status.Should().Be(SagaStatus.Running);
        saga.CurrentStepIndex.Should().Be(0);
        saga.Version.Should().Be(0);
        saga.Steps.Should().HaveCount(SagaInstance.StepCount);
        saga.Steps.Select(step => step.Kind).Should().Equal(
            SagaStepKind.ValidateQuote,
            SagaStepKind.ReserveInventory,
            SagaStepKind.AuthorizePayment,
            SagaStepKind.BurnCredits,
            SagaStepKind.CapturePayment,
            SagaStepKind.ConfirmBooking);
        saga.Steps.Should().OnlyContain(step => step.Status == SagaStepStatus.Pending);
        saga.Steps.Should().OnlyContain(step =>
            step.IdempotencyKey == SagaInstance.DeriveIdempotencyKey(id, step.Kind));
    }

    [Fact]
    public void Idempotency_key_is_stable_for_the_same_saga_and_step()
    {
        var id = new SagaInstanceId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

        var first = SagaInstance.DeriveIdempotencyKey(id, SagaStepKind.AuthorizePayment);
        var second = SagaInstance.DeriveIdempotencyKey(id, SagaStepKind.AuthorizePayment);

        first.Should().Be("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee:AuthorizePayment");
        second.Should().Be(first);
        SagaInstance.DeriveIdempotencyKey(id, SagaStepKind.CapturePayment)
            .Should().NotBe(first);
        SagaInstance.DeriveCompensationKey(id, SagaStepKind.AuthorizePayment)
            .Should().Be("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee:AuthorizePayment:compensate");
    }

    [Fact]
    public void MarkInProgress_persists_the_attempt_before_any_call_out()
    {
        var saga = Start();
        var clock = new Clock(AsOf);

        saga.MarkInProgress(SagaStepKind.ValidateQuote, clock);

        saga.StepStatus(SagaStepKind.ValidateQuote).Should().Be(SagaStepStatus.InProgress);
        saga.Step(SagaStepKind.ValidateQuote).Attempts.Should().Be(1);
        saga.LastHeartbeatAt.Should().Be(AsOf);
        saga.Version.Should().Be(1);
    }

    [Fact]
    public void Advance_after_the_last_success_confirms_the_saga()
    {
        var saga = Start();
        var clock = new Clock(AsOf);

        foreach (var kind in Enum.GetValues<SagaStepKind>().OrderBy(k => (int)k))
        {
            saga.MarkInProgress(kind, clock);
            saga.MarkSucceeded(kind, $"{kind}-ref", clock);
            saga.Advance(clock);
        }

        saga.Status.Should().Be(SagaStatus.Confirmed);
        saga.CompletedAt.Should().Be(AsOf);
        saga.CurrentStepIndex.Should().Be(SagaInstance.StepCount - 1);
    }

    [Fact]
    public void Compensation_marks_completed_steps_then_terminates_compensated()
    {
        var saga = Start();
        var clock = new Clock(AsOf);
        saga.MarkInProgress(SagaStepKind.ValidateQuote, clock);
        saga.MarkSucceeded(SagaStepKind.ValidateQuote, null, clock);
        saga.Advance(clock);
        saga.MarkInProgress(SagaStepKind.ReserveInventory, clock);
        saga.MarkFailed(SagaStepKind.ReserveInventory, Errors.SupplierUnavailable, clock);

        saga.BeginCompensation(clock);
        saga.MarkStepCompensated(
            SagaStepKind.ValidateQuote,
            new CompensationRecord(CompensationStatus.Succeeded, null, null, 1, AsOf),
            clock);
        saga.CompleteCompensation(clock);

        saga.Status.Should().Be(SagaStatus.Compensated);
        saga.StepStatus(SagaStepKind.ValidateQuote).Should().Be(SagaStepStatus.Compensated);
        saga.StepStatus(SagaStepKind.ReserveInventory).Should().Be(SagaStepStatus.Failed);
        saga.CompletedAt.Should().Be(AsOf);
    }

    [Fact]
    public void Exhausted_compensation_requires_manual_review()
    {
        var saga = Start();
        var clock = new Clock(AsOf);
        saga.MarkInProgress(SagaStepKind.ValidateQuote, clock);
        saga.MarkSucceeded(SagaStepKind.ValidateQuote, null, clock);
        saga.Advance(clock);
        saga.MarkInProgress(SagaStepKind.ReserveInventory, clock);
        saga.MarkFailed(SagaStepKind.ReserveInventory, Errors.SupplierUnavailable, clock);

        saga.BeginCompensation(clock);
        saga.RequireManualReview(
            SagaStepKind.ValidateQuote,
            new CompensationRecord(CompensationStatus.Failed, null, Errors.SupplierUnavailable, 5, AsOf),
            clock);

        saga.Status.Should().Be(SagaStatus.RequiresManualReview);
        saga.StepStatus(SagaStepKind.ValidateQuote).Should().Be(SagaStepStatus.CompensationFailed);
    }

    [Fact]
    public void IsStalled_when_heartbeat_is_older_than_the_partner_threshold()
    {
        var saga = Start();
        var clock = new Clock(AsOf.AddSeconds(60));

        saga.IsStalled(new SagaPolicy(10, 3, 5, 60), clock).Should().BeTrue();
        saga.IsStalled(new SagaPolicy(10, 3, 5, 61), clock).Should().BeFalse();
    }

    [Fact]
    public void Terminal_sagas_are_not_stalled()
    {
        var saga = Start();
        var clock = new Clock(AsOf);
        saga.MarkInProgress(SagaStepKind.ValidateQuote, clock);
        saga.MarkSucceeded(SagaStepKind.ValidateQuote, null, clock);
        saga.Advance(clock);
        saga.MarkFailed(SagaStepKind.ReserveInventory, Errors.SupplierUnavailable, clock);
        saga.BeginCompensation(clock);
        saga.MarkStepCompensated(
            SagaStepKind.ValidateQuote,
            new CompensationRecord(CompensationStatus.Succeeded, null, null, 1, AsOf),
            clock);
        saga.CompleteCompensation(clock);

        saga.IsStalled(new SagaPolicy(10, 3, 5, 1), new Clock(AsOf.AddHours(1))).Should().BeFalse();
    }

    private static SagaInstance Start(SagaInstanceId? id = null) =>
        SagaInstance.Start(PartnerId.New(), BookingId.New(), Checkout(), "corr-1", new Clock(AsOf), id);

    private static SagaCheckout Checkout() =>
        new(
            QuoteId.New(),
            new TenderSplit(Money.Of(1.00m, Currency.Usd), 0, Money.Of(0m, Currency.Usd)),
            new DateOnly(2026, 6, 1),
            Percent.From(5m));

    private sealed class Clock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
