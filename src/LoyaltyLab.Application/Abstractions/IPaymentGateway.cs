using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Application.Abstractions;

public sealed record PaymentAuthorizeRequest(Money Amount, string IdempotencyKey, string? Description);

public sealed record PaymentReferenceRequest(string PaymentId, string IdempotencyKey);

public interface IPaymentGateway
{
    Task<StepOutcome> AuthorizeAsync(PaymentAuthorizeRequest request, CancellationToken cancellationToken);

    Task<StepOutcome> CaptureAsync(PaymentReferenceRequest request, CancellationToken cancellationToken);

    Task<StepOutcome> VoidAsync(PaymentReferenceRequest request, CancellationToken cancellationToken);

    Task<StepOutcome> RefundAsync(PaymentReferenceRequest request, CancellationToken cancellationToken);

    Task<StepOutcome> QueryByKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
}
