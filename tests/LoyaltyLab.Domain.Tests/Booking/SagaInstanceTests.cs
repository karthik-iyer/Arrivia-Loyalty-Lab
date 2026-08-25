using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;

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

    private static SagaInstance Start(SagaInstanceId? id = null) =>
        SagaInstance.Start(PartnerId.New(), BookingId.New(), "corr-1", new Clock(AsOf), id);

    private sealed class Clock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
