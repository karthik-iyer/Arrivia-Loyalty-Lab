using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Application.Booking;

public sealed class ConfirmBookingStep(
    IBookingRepository bookings,
    EarnCredits earn,
    ReverseLedger reverse) : ISagaStep
{
    public SagaStepKind Kind => SagaStepKind.ConfirmBooking;

    public int Order => (int)Kind;

    public async Task<StepOutcome> ExecuteAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var booking = await EnsureConfirmedAsync(context, cancellationToken);
        var credits = EarnCreditsFor(context);
        if (credits <= 0)
        {
            return StepOutcome.Succeeded();
        }

        var posted = await earn.ExecuteAsync(
            new EarnCreditsCommand(
                context.Member.Id,
                credits,
                context.Key(Kind),
                "Booking earn",
                context.Saga.BookingId),
            cancellationToken);
        return posted.IsSuccess
            ? StepOutcome.Succeeded(posted.Value.Transaction.Id.ToString())
            : StepOutcome.Failed(posted.Error);
    }

    public async Task<CompensationOutcome> CompensateAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var booking = await bookings.GetByIdAsync(context.Saga.BookingId, cancellationToken);
        booking?.Cancel();

        var reference = context.Reference(Kind);
        if (string.IsNullOrWhiteSpace(reference) || !Guid.TryParse(reference, out var id))
        {
            return CompensationOutcome.Ok();
        }

        var reversed = await reverse.ExecuteAsync(
            new ReverseLedgerCommand(
                new LedgerTransactionId(id),
                context.CompensateKey(Kind),
                "Compensate booking earn"),
            cancellationToken);
        return reversed.IsSuccess
            ? CompensationOutcome.Ok(reversed.Value.Transaction.Id.ToString())
            : CompensationOutcome.Fail(reversed.Error);
    }

    public Task<StepOutcome> ResolveUnknownAsync(SagaContext context, CancellationToken cancellationToken) =>
        ExecuteAsync(context, cancellationToken);

    private async Task<LoyaltyLab.Domain.Booking.Booking> EnsureConfirmedAsync(
        SagaContext context,
        CancellationToken cancellationToken)
    {
        var existing = await bookings.GetByIdAsync(context.Saga.BookingId, cancellationToken);
        if (existing is null)
        {
            existing = LoyaltyLab.Domain.Booking.Booking.Place(
                context.Saga.BookingId,
                context.Partner.Id,
                context.Member.Id,
                context.Quote.Id,
                context.Tender);
            await bookings.AddAsync(existing, cancellationToken);
        }

        existing.Confirm(context.Reference(SagaStepKind.ReserveInventory), context.Drift);
        return existing;
    }

    private static int EarnCreditsFor(SagaContext context)
    {
        var margin = context.Quote.MemberPrice - context.Quote.NetCostSnapshot;
        if (margin.IsNegative || margin.IsZero)
        {
            return 0;
        }

        var earnMoney = Money.Of(
                margin.Amount * context.Partner.CreditPolicy.EarnRateOnMargin.AsFraction(),
                margin.Currency)
            .RoundToCents();
        return context.Partner.CreditPolicy.ToCredits(earnMoney);
    }
}
