using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Application.Loyalty;

public sealed class ExpireCredits(
    ITenantContextAccessor tenant,
    IMemberRepository members,
    IPartnerRepository partners,
    ILedgerRepository ledger,
    IUnitOfWork unitOfWork,
    ClaimIdempotency claim,
    IClock clock) : IUseCase<ExpireCreditsCommand, LedgerPostingResult>
{
    private readonly LedgerMutationSupport _support = new(tenant, members, partners, ledger, unitOfWork, claim);

    public async Task<Result<LedgerPostingResult>> ExecuteAsync(
        ExpireCreditsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await _support.RequireMemberAndPartnerAsync(request.MemberId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<LedgerPostingResult>.Failure(loaded.Error);
        }

        var memberCredits = await _support.EnsureAccountAsync(
            LedgerAccountType.MemberCredits,
            request.MemberId,
            cancellationToken);
        var breakage = await _support.EnsureAccountAsync(
            LedgerAccountType.PartnerBreakage,
            memberId: null,
            cancellationToken);

        var balance = await _support.MemberBalanceAsync(memberCredits, cancellationToken);
        if (request.Credits > balance)
        {
            return Result<LedgerPostingResult>.Failure(Errors.InsufficientCredits);
        }

        return await _support.CommitAsync(
            LedgerOp.Expire,
            request.IdempotencyKey,
            $"{request.MemberId.Value:N}|{request.Credits}|{request.Reason}",
            _ => Task.FromResult(
                Result<LedgerTransaction>.Success(
                    LedgerTransaction.Expire(
                        memberCredits,
                        breakage,
                        request.Credits,
                        request.IdempotencyKey,
                        request.Reason,
                        clock))),
            cancellationToken);
    }
}
