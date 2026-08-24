using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Loyalty;

internal static class LedgerOp
{
    public const string Earn = "Earn";
    public const string Burn = "Burn";
    public const string Expire = "Expire";
    public const string Reverse = "Reverse";
    public const string Adjust = "Adjust";
}

internal sealed class LedgerMutationSupport(
    ITenantContextAccessor tenant,
    IMemberRepository members,
    IPartnerRepository partners,
    ILedgerRepository ledger,
    IUnitOfWork unitOfWork,
    ClaimIdempotency claim)
{
    public async Task<Result<(Member Member, Partner Partner)>> RequireMemberAndPartnerAsync(
        MemberId memberId,
        CancellationToken cancellationToken)
    {
        var member = await members.GetByIdAsync(memberId, cancellationToken);
        if (member is null || member.PartnerId != tenant.Current.PartnerId)
        {
            return Result<(Member, Partner)>.Failure(Errors.MemberNotFound);
        }

        var partner = await partners.GetByIdAsync(tenant.Current.PartnerId, cancellationToken);
        if (partner is null)
        {
            return Result<(Member, Partner)>.Failure(Errors.MemberNotFound);
        }

        return Result<(Member, Partner)>.Success((member, partner));
    }

    public async Task<LedgerAccount> EnsureAccountAsync(
        LedgerAccountType type,
        MemberId? memberId,
        CancellationToken cancellationToken)
    {
        var existing = await ledger.FindAccountAsync(
            tenant.Current.PartnerId,
            type,
            memberId,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = type switch
        {
            LedgerAccountType.MemberCredits => LedgerAccount.MemberCredits(
                tenant.Current.PartnerId,
                memberId ?? throw new DomainException("A member-credits account must name a member.")),
            LedgerAccountType.PartnerIssuance => LedgerAccount.Issuance(tenant.Current.PartnerId),
            LedgerAccountType.PartnerRedemption => LedgerAccount.Redemption(tenant.Current.PartnerId),
            LedgerAccountType.PartnerBreakage => LedgerAccount.Breakage(tenant.Current.PartnerId),
            _ => throw new DomainException($"Unknown ledger account type {type}."),
        };

        await ledger.AddAccountAsync(created, cancellationToken);
        return created;
    }

    public async Task<int> MemberBalanceAsync(LedgerAccount memberCredits, CancellationToken cancellationToken)
    {
        var history = await ledger.ListAsync(cancellationToken);
        return LedgerBalances.For(memberCredits.Id, history);
    }

    public async Task<IReadOnlyList<LedgerTransaction>> HistoryAsync(CancellationToken cancellationToken) =>
        await ledger.ListAsync(cancellationToken);

    public async Task<Result<LedgerPostingResult>> CommitAsync(
        string operation,
        string idempotencyKey,
        string payload,
        Func<CancellationToken, Task<Result<LedgerTransaction>>> post,
        CancellationToken cancellationToken)
    {
        var claimed = await claim.ExecuteAsync(
            new ClaimIdempotencyCommand(operation, idempotencyKey, payload),
            cancellationToken);
        if (claimed.IsFailure)
        {
            return Result<LedgerPostingResult>.Failure(claimed.Error);
        }

        if (claimed.Value.IsReplay)
        {
            var replayed = await ledger.FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            if (replayed is null)
            {
                throw new DomainException("Idempotency replay found no ledger posting for this key.");
            }

            return Result<LedgerPostingResult>.Success(new LedgerPostingResult(replayed, IsReplay: true));
        }

        var posted = await post(cancellationToken);
        if (posted.IsFailure)
        {
            return Result<LedgerPostingResult>.Failure(posted.Error);
        }

        await ledger.AddAsync(posted.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<LedgerPostingResult>.Success(new LedgerPostingResult(posted.Value, IsReplay: false));
    }
}
