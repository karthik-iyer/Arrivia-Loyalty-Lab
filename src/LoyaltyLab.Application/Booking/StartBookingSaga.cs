using System.Security.Cryptography;
using System.Text;
using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Booking;

/// <summary>
/// Starts checkout as a saga. The idempotency key derives a stable booking id so a
/// replay after a crash continues the same instance (FR-B-03, FR-B-12).
/// </summary>
public sealed class StartBookingSaga(
    ITenantContextAccessor tenant,
    IQuoteRepository quotes,
    IOfferRepository offers,
    IMemberRepository members,
    IPartnerRepository partners,
    IPricingRuleRepository rules,
    IBookingRepository bookings,
    ISagaRepository sagas,
    ClaimIdempotency claim,
    GetBalance getBalance,
    AdvanceSaga advance,
    IUnitOfWork unitOfWork,
    IClock clock) : IUseCase<StartBookingSagaCommand, BookingResult>
{
    public const string Operation = "bookings.start";

    public async Task<Result<BookingResult>> ExecuteAsync(
        StartBookingSagaCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Result<BookingResult>.Failure(Errors.MissingIdempotencyKey);
        }

        var context = tenant.Current;
        if (!context.HasMember || context.MemberId is not { } memberId)
        {
            return Result<BookingResult>.Failure(Errors.MemberNotFound);
        }

        var claimed = await claim.ExecuteAsync(
            new ClaimIdempotencyCommand(
                Operation,
                request.IdempotencyKey,
                $"{request.QuoteId.Value:N}|{request.Credits}|{request.StayDate:yyyy-MM-dd}"),
            cancellationToken);
        if (claimed.IsFailure)
        {
            return Result<BookingResult>.Failure(claimed.Error);
        }

        var bookingId = BookingRequestIds.FromStartKey(context.PartnerId, request.IdempotencyKey);
        var existing = await bookings.GetByIdAsync(bookingId, cancellationToken);
        if (existing is not null)
        {
            return await ResumeAsync(existing, cancellationToken);
        }

        var quote = await quotes.GetByIdAsync(request.QuoteId, cancellationToken);
        if (quote is null || quote.MemberId != memberId)
        {
            return Result<BookingResult>.Failure(Errors.QuoteNotFound);
        }

        var other = await bookings.FindByQuoteIdAsync(quote.Id, cancellationToken);
        if (other is not null && other.Status is BookingStatus.Pending or BookingStatus.Confirmed)
        {
            return Result<BookingResult>.Failure(Errors.BookingInProgress);
        }

        if (quote.IsExpired(clock))
        {
            return Result<BookingResult>.Failure(Errors.QuoteExpired);
        }

        var member = await members.GetByIdAsync(memberId, cancellationToken);
        var partner = await partners.GetByIdAsync(context.PartnerId, cancellationToken);
        var offer = await offers.GetByIdAsync(quote.OfferId, cancellationToken);
        if (member is null || partner is null || offer is null)
        {
            return Result<BookingResult>.Failure(Errors.QuoteNotFound);
        }

        var tender = await SplitTenderAsync(request.Credits, quote, partner, cancellationToken);
        if (tender.IsFailure)
        {
            return Result<BookingResult>.Failure(tender.Error);
        }

        var floor = await FloorAboveNetAsync(partner.Id, cancellationToken);
        var booking = Domain.Booking.Booking.Place(bookingId, partner.Id, member.Id, quote.Id, tender.Value);
        var saga = SagaInstance.Start(
            partner.Id,
            bookingId,
            new SagaCheckout(quote.Id, tender.Value, request.StayDate, floor),
            string.IsNullOrWhiteSpace(request.CorrelationId)
                ? bookingId.Value.ToString("D")
                : request.CorrelationId.Trim(),
            clock);

        await bookings.AddAsync(booking, cancellationToken);
        await sagas.AddAsync(saga, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var sagaContext = new SagaContext
        {
            Saga = saga,
            Quote = quote,
            Offer = offer,
            Partner = partner,
            Member = member,
            Tender = tender.Value,
            StayDate = request.StayDate,
            FloorAboveNet = floor,
        };
        await advance.ExecuteAsync(sagaContext, cancellationToken);
        if (saga.Status == SagaStatus.Compensated && booking.Status == BookingStatus.Pending)
        {
            booking.Fail();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<BookingResult>.Success(BookingResult.From(booking, saga));
    }

    private async Task<Result<BookingResult>> ResumeAsync(
        Domain.Booking.Booking booking,
        CancellationToken cancellationToken)
    {
        var saga = await sagas.GetByBookingIdAsync(booking.Id, cancellationToken);
        if (saga is null)
        {
            return Result<BookingResult>.Failure(Errors.BookingNotFound);
        }

        if (saga.Status is SagaStatus.Running or SagaStatus.Compensating)
        {
            var context = await LoadContextAsync(saga, cancellationToken);
            if (context is not null)
            {
                await advance.ExecuteAsync(context, cancellationToken);
                if (saga.Status == SagaStatus.Compensated && booking.Status == BookingStatus.Pending)
                {
                    booking.Fail();
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return Result<BookingResult>.Success(BookingResult.From(booking, saga));
    }

    private async Task<SagaContext?> LoadContextAsync(SagaInstance saga, CancellationToken cancellationToken)
    {
        var quote = await quotes.GetByIdAsync(saga.Checkout.QuoteId, cancellationToken);
        var partner = await partners.GetByIdAsync(saga.PartnerId, cancellationToken);
        if (quote is null || partner is null)
        {
            return null;
        }

        var member = await members.GetByIdAsync(quote.MemberId, cancellationToken);
        var offer = await offers.GetByIdAsync(quote.OfferId, cancellationToken);
        if (member is null || offer is null)
        {
            return null;
        }

        return new SagaContext
        {
            Saga = saga,
            Quote = quote,
            Offer = offer,
            Partner = partner,
            Member = member,
            Tender = saga.Checkout.Tender,
            StayDate = saga.Checkout.StayDate,
            FloorAboveNet = saga.Checkout.FloorAboveNet,
        };
    }

    private async Task<Result<TenderSplit>> SplitTenderAsync(
        int credits,
        Quote quote,
        Partner partner,
        CancellationToken cancellationToken)
    {
        if (credits < 0)
        {
            return Result<TenderSplit>.Failure(Errors.BurnCapExceeded);
        }

        var creditValue = partner.CreditPolicy.ToMoney(credits, quote.MemberPrice.Currency).RoundToCents();
        if (creditValue > quote.MaxCreditTender)
        {
            return Result<TenderSplit>.Failure(Errors.BurnCapExceeded);
        }

        var cash = (quote.MemberPrice - creditValue).RoundToCents();
        if (cash.IsNegative)
        {
            return Result<TenderSplit>.Failure(Errors.BurnCapExceeded);
        }

        if (credits > 0)
        {
            var balance = await getBalance.ExecuteAsync(new GetBalanceQuery(), cancellationToken);
            if (balance.IsFailure)
            {
                return Result<TenderSplit>.Failure(balance.Error);
            }

            if (credits > balance.Value.Credits)
            {
                return Result<TenderSplit>.Failure(Errors.InsufficientCredits);
            }
        }

        return Result<TenderSplit>.Success(new TenderSplit(cash, credits, creditValue));
    }

    private async Task<Percent> FloorAboveNetAsync(PartnerId partnerId, CancellationToken cancellationToken)
    {
        var asOf = clock.UtcNow;
        var partnerRules = await rules.ListForPartnerAsync(partnerId, asOf, cancellationToken);
        var floor = partnerRules
            .OfType<MarginFloorRule>()
            .Where(rule => asOf >= rule.EffectiveFrom && (rule.EffectiveTo is null || asOf < rule.EffectiveTo.Value))
            .OrderByDescending(rule => rule.Specificity)
            .ThenByDescending(rule => rule.Priority)
            .FirstOrDefault();
        return floor?.FloorAboveNet ?? Percent.Zero;
    }
}

internal static class BookingRequestIds
{
    public static BookingId FromStartKey(PartnerId partnerId, string idempotencyKey)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes($"bookings.start|{partnerId.Value:N}|{idempotencyKey.Trim()}"));
        return new BookingId(new Guid(bytes.AsSpan(0, 16)));
    }
}

internal static class BookingVisibility
{
    public static bool CanView(TenantContext tenant, MemberId owner) =>
        tenant.Role is AccessRole.Operator
        || (tenant.HasMember && tenant.MemberId == owner);
}
