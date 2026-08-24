using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Application.Loyalty;

public sealed class AdjustCredits(
    ITenantContextAccessor tenant,
    IMemberRepository members,
    IPartnerRepository partners,
    ILedgerRepository ledger,
    IUnitOfWork unitOfWork,
    ClaimIdempotency claim,
    IClock clock) : IUseCase<AdjustCreditsCommand, LedgerPostingResult>
{
    private readonly LedgerMutationSupport _support = new(tenant, members, partners, ledger, unitOfWork, claim);

    public async Task<Result<LedgerPostingResult>> ExecuteAsync(
        AdjustCreditsCommand request,
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
        var issuance = await _support.EnsureAccountAsync(
            LedgerAccountType.PartnerIssuance,
            memberId: null,
            cancellationToken);

        if (request.Credits < 0)
        {
            var balance = await _support.MemberBalanceAsync(memberCredits, cancellationToken);
            if (-request.Credits > balance)
            {
                return Result<LedgerPostingResult>.Failure(Errors.InsufficientCredits);
            }
        }

        return await _support.CommitAsync(
            LedgerOp.Adjust,
            request.IdempotencyKey,
            $"{request.MemberId.Value:N}|{request.Credits}|{request.Reason}",
            _ => Task.FromResult(
                Result<LedgerTransaction>.Success(
                    LedgerTransaction.Adjust(
                        memberCredits,
                        issuance,
                        request.Credits,
                        request.IdempotencyKey,
                        request.Reason,
                        clock))),
            cancellationToken);
    }
}
