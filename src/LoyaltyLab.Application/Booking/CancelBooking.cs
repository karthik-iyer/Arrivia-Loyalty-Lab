using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Booking;

/// <summary>
/// Full cancellation of a confirmed booking. Reverses the original ledger amounts
/// (FR-L-08), refunds the capture, and releases the supplier hold.
/// </summary>
public sealed class CancelBooking(
    ITenantContextAccessor tenant,
    IBookingRepository bookings,
    ISagaRepository sagas,
    ClaimIdempotency claim,
    ReverseLedger reverse,
    IPaymentGateway payments,
    ISupplierClient supplier,
    IUnitOfWork unitOfWork) : IUseCase<CancelBookingCommand, BookingResult>
{
    public const string Operation = "bookings.cancel";

    public async Task<Result<BookingResult>> ExecuteAsync(
        CancelBookingCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Result<BookingResult>.Failure(Errors.MissingIdempotencyKey);
        }

        var booking = await bookings.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null || !BookingVisibility.CanView(tenant.Current, booking.MemberId))
        {
            return Result<BookingResult>.Failure(Errors.BookingNotFound);
        }

        var saga = await sagas.GetByBookingIdAsync(booking.Id, cancellationToken);
        if (saga is null)
        {
            return Result<BookingResult>.Failure(Errors.BookingNotFound);
        }

        var claimed = await claim.ExecuteAsync(
            new ClaimIdempotencyCommand(Operation, request.IdempotencyKey, booking.Id.Value.ToString("N")),
            cancellationToken);
        if (claimed.IsFailure)
        {
            return Result<BookingResult>.Failure(claimed.Error);
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return claimed.Value.IsReplay
                ? Result<BookingResult>.Success(BookingResult.From(booking, saga))
                : Result<BookingResult>.Failure(Errors.BookingAlreadyCancelled);
        }

        if (booking.Status != BookingStatus.Confirmed)
        {
            return Result<BookingResult>.Failure(
                saga.Status is SagaStatus.Running or SagaStatus.Compensating
                    ? Errors.BookingInProgress
                    : Errors.BookingNotFound);
        }

        var unwind = await UnwindAsync(saga, cancellationToken);
        if (unwind.IsFailure)
        {
            return Result<BookingResult>.Failure(unwind.Error);
        }

        booking.Cancel();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<BookingResult>.Success(BookingResult.From(booking, saga));
    }

    private async Task<Result<bool>> UnwindAsync(SagaInstance saga, CancellationToken cancellationToken)
    {
        var paymentId = saga.Step(SagaStepKind.CapturePayment).ExternalReference
            ?? saga.Step(SagaStepKind.AuthorizePayment).ExternalReference;
        if (!string.IsNullOrWhiteSpace(paymentId))
        {
            var refunded = await payments.RefundAsync(
                new PaymentReferenceRequest(paymentId, $"{saga.Id.Value:D}:cancel:refund"),
                cancellationToken);
            if (refunded.Result != StepResult.Succeeded)
            {
                return Result<bool>.Failure(refunded.Error ?? Errors.PaymentDeclined);
            }
        }

        var earnId = TryLedgerId(saga.Step(SagaStepKind.ConfirmBooking).ExternalReference);
        if (earnId is { } earn)
        {
            var reversedEarn = await reverse.ExecuteAsync(
                new ReverseLedgerCommand(earn, $"{saga.Id.Value:D}:cancel:earn", "Cancel booking earn"),
                cancellationToken);
            if (reversedEarn.IsFailure)
            {
                return Result<bool>.Failure(reversedEarn.Error);
            }
        }

        var burnId = TryLedgerId(saga.Step(SagaStepKind.BurnCredits).ExternalReference);
        if (burnId is { } burn)
        {
            var reversedBurn = await reverse.ExecuteAsync(
                new ReverseLedgerCommand(burn, $"{saga.Id.Value:D}:cancel:burn", "Cancel booking burn"),
                cancellationToken);
            if (reversedBurn.IsFailure)
            {
                return Result<bool>.Failure(reversedBurn.Error);
            }
        }

        var reservation = saga.Step(SagaStepKind.ReserveInventory).ExternalReference;
        if (!string.IsNullOrWhiteSpace(reservation))
        {
            var released = await supplier.ReleaseAsync(reservation, cancellationToken);
            if (released.Result != StepResult.Succeeded)
            {
                return Result<bool>.Failure(released.Error ?? Errors.SupplierUnavailable);
            }
        }

        return Result<bool>.Success(true);
    }

    private static LedgerTransactionId? TryLedgerId(string? reference) =>
        Guid.TryParse(reference, out var id) ? new LedgerTransactionId(id) : null;
}
