using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Application.Loyalty;

public sealed class EarnCredits(
    ITenantContextAccessor tenant,
    IMemberRepository members,
    IPartnerRepository partners,
    ILedgerRepository ledger,
    IUnitOfWork unitOfWork,
    ClaimIdempotency claim,
    IClock clock) : IUseCase<EarnCreditsCommand, LedgerPostingResult>
{
    private readonly LedgerMutationSupport _support = new(tenant, members, partners, ledger, unitOfWork, claim);

    public async Task<Result<LedgerPostingResult>> ExecuteAsync(
        EarnCreditsCommand request,
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

        return await _support.CommitAsync(
            LedgerOp.Earn,
            request.IdempotencyKey,
            $"{request.MemberId.Value:N}|{request.Credits}|{request.BookingId?.Value:N}|{request.Reason}",
            _ => Task.FromResult(
                Result<LedgerTransaction>.Success(
                    LedgerTransaction.Earn(
                        memberCredits,
                        issuance,
                        request.Credits,
                        request.IdempotencyKey,
                        request.Reason,
                        clock,
                        request.BookingId))),
            cancellationToken);
    }
}
