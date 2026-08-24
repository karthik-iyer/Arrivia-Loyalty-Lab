using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Application.Loyalty;

public sealed class ReverseLedger(
    ITenantContextAccessor tenant,
    IMemberRepository members,
    IPartnerRepository partners,
    ILedgerRepository ledger,
    IUnitOfWork unitOfWork,
    ClaimIdempotency claim,
    IClock clock) : IUseCase<ReverseLedgerCommand, LedgerPostingResult>
{
    private readonly LedgerMutationSupport _support = new(tenant, members, partners, ledger, unitOfWork, claim);

    public async Task<Result<LedgerPostingResult>> ExecuteAsync(
        ReverseLedgerCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var original = await ledger.GetByIdAsync(request.OriginalId, cancellationToken);
        if (original is null)
        {
            return Result<LedgerPostingResult>.Failure(Errors.LedgerTransactionNotFound);
        }

        var existing = await ledger.FindByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (existing is null)
        {
            if (await IsAlreadyReversedAsync(original, cancellationToken))
            {
                return Result<LedgerPostingResult>.Failure(Errors.TransactionAlreadyReversed);
            }

            if (await WouldMakeMemberNegativeAsync(original, cancellationToken))
            {
                return Result<LedgerPostingResult>.Failure(Errors.InsufficientCredits);
            }
        }

        return await _support.CommitAsync(
            LedgerOp.Reverse,
            request.IdempotencyKey,
            $"{request.OriginalId.Value:N}|{request.Reason}",
            async _ =>
            {
                if (await IsAlreadyReversedAsync(original, cancellationToken))
                {
                    return Result<LedgerTransaction>.Failure(Errors.TransactionAlreadyReversed);
                }

                return Result<LedgerTransaction>.Success(
                    LedgerTransaction.Reverse(original, request.IdempotencyKey, request.Reason, clock));
            },
            cancellationToken);
    }

    private async Task<bool> IsAlreadyReversedAsync(
        LedgerTransaction original,
        CancellationToken cancellationToken)
    {
        if (original.Type == LedgerTransactionType.Reversal)
        {
            return true;
        }

        var history = await _support.HistoryAsync(cancellationToken);
        return history.Any(transaction =>
            transaction.Type == LedgerTransactionType.Reversal
            && transaction.ReversesTransactionId == original.Id);
    }

    private async Task<bool> WouldMakeMemberNegativeAsync(
        LedgerTransaction original,
        CancellationToken cancellationToken)
    {
        var history = await _support.HistoryAsync(cancellationToken);
        foreach (var entry in original.Entries)
        {
            var account = await ledger.GetAccountAsync(entry.AccountId, cancellationToken);
            if (account is not { Type: LedgerAccountType.MemberCredits })
            {
                continue;
            }

            if (LedgerBalances.For(account.Id, history) - entry.Amount < 0)
            {
                return true;
            }
        }

        return false;
    }
}
