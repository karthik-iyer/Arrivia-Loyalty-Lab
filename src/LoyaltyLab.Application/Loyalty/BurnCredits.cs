using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Application.Loyalty;

public sealed class BurnCredits(
    ITenantContextAccessor tenant,
    IMemberRepository members,
    IPartnerRepository partners,
    ILedgerRepository ledger,
    IUnitOfWork unitOfWork,
    ClaimIdempotency claim,
    IClock clock) : IUseCase<BurnCreditsCommand, LedgerPostingResult>
{
    private readonly LedgerMutationSupport _support = new(tenant, members, partners, ledger, unitOfWork, claim);

    public async Task<Result<LedgerPostingResult>> ExecuteAsync(
        BurnCreditsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await _support.RequireMemberAndPartnerAsync(request.MemberId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<LedgerPostingResult>.Failure(loaded.Error);
        }

        var (_, partner) = loaded.Value;
        if (request.MemberPrice.Currency != partner.Currency)
        {
            throw new DomainException("Burn cap is evaluated in the partner currency.");
        }

        var capMoney = request.MemberPrice
            .Multiply(partner.CreditPolicy.DefaultBurnCap.AsFraction())
            .RoundToCents();
        var maxCredits = partner.CreditPolicy.ToCredits(capMoney);
        if (request.Credits > maxCredits)
        {
            return Result<LedgerPostingResult>.Failure(Errors.BurnCapExceeded);
        }

        var memberCredits = await _support.EnsureAccountAsync(
            LedgerAccountType.MemberCredits,
            request.MemberId,
            cancellationToken);
        var redemption = await _support.EnsureAccountAsync(
            LedgerAccountType.PartnerRedemption,
            memberId: null,
            cancellationToken);

        var balance = await _support.MemberBalanceAsync(memberCredits, cancellationToken);
        if (request.Credits > balance)
        {
            return Result<LedgerPostingResult>.Failure(Errors.InsufficientCredits);
        }

        return await _support.CommitAsync(
            LedgerOp.Burn,
            request.IdempotencyKey,
            $"{request.MemberId.Value:N}|{request.Credits}|{request.MemberPrice}|{request.BookingId?.Value:N}|{request.Reason}",
            _ => Task.FromResult(
                Result<LedgerTransaction>.Success(
                    LedgerTransaction.Burn(
                        memberCredits,
                        redemption,
                        request.Credits,
                        request.IdempotencyKey,
                        request.Reason,
                        clock,
                        request.BookingId))),
            cancellationToken);
    }
}
