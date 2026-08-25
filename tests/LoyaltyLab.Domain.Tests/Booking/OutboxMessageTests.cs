using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Booking;

public sealed class OutboxMessageTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_records_occurred_at_from_the_clock()
    {
        var partner = PartnerId.New();
        var message = OutboxMessage.Create(
            partner,
            OutboxMessageTypes.BookingConfirmed,
            """{"sagaId":"s"}""",
            "corr-1",
            new Clock(AsOf));

        message.PartnerId.Should().Be(partner);
        message.Type.Should().Be(OutboxMessageTypes.BookingConfirmed);
        message.CorrelationId.Should().Be("corr-1");
        message.OccurredAt.Should().Be(AsOf);
        message.Attempts.Should().Be(0);
        message.IsDispatched.Should().BeFalse();
    }

    [Fact]
    public void RecordAttempt_increments_and_stores_the_error()
    {
        var message = Message();

        message.RecordAttempt("boom");

        message.Attempts.Should().Be(1);
        message.LastError.Should().Be("boom");
    }

    [Fact]
    public void MarkDispatched_is_idempotent()
    {
        var message = Message();
        var clock = new Clock(AsOf);

        message.MarkDispatched(clock);
        message.MarkDispatched(new Clock(AsOf.AddMinutes(1)));

        message.DispatchedAt.Should().Be(AsOf);
        message.IsDispatched.Should().BeTrue();
    }

    [Fact]
    public void PoisonMessage_copies_the_exhausted_outbox_row()
    {
        var message = Message();
        message.RecordAttempt("still failing");

        var poison = PoisonMessage.From(message, new Clock(AsOf.AddSeconds(5)));

        poison.OutboxMessageId.Should().Be(message.Id);
        poison.PartnerId.Should().Be(message.PartnerId);
        poison.Type.Should().Be(message.Type);
        poison.Payload.Should().Be(message.Payload);
        poison.CorrelationId.Should().Be(message.CorrelationId);
        poison.OccurredAt.Should().Be(message.OccurredAt);
        poison.Attempts.Should().Be(1);
        poison.LastError.Should().Be("still failing");
        poison.PoisonedAt.Should().Be(AsOf.AddSeconds(5));
    }

    [Fact]
    public void Create_rejects_a_blank_type()
    {
        var act = () => OutboxMessage.Create(
            PartnerId.New(),
            "  ",
            "{}",
            "corr-1",
            new Clock(AsOf));

        act.Should().Throw<DomainException>();
    }

    private static OutboxMessage Message() =>
        OutboxMessage.Create(
            PartnerId.New(),
            OutboxMessageTypes.CreditsBurned,
            """{"bookingId":"b"}""",
            "corr-1",
            new Clock(AsOf));

    private sealed class Clock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
