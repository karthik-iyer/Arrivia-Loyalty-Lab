using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Loyalty;

public sealed class GetBalance(
    ITenantContextAccessor tenant,
    IMemberRepository members,
    IPartnerRepository partners,
    ILedgerRepository ledger) : IUseCase<GetBalanceQuery, MemberBalance>
{
    public async Task<Result<MemberBalance>> ExecuteAsync(
        GetBalanceQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!tenant.Current.HasMember || tenant.Current.MemberId is not { } memberId)
        {
            return Result<MemberBalance>.Failure(Errors.MemberNotFound);
        }

        var member = await members.GetByIdAsync(memberId, cancellationToken);
        var partner = await partners.GetByIdAsync(tenant.Current.PartnerId, cancellationToken);
        if (member is null || partner is null)
        {
            return Result<MemberBalance>.Failure(Errors.MemberNotFound);
        }

        var account = await ledger.FindAccountAsync(
            partner.Id,
            LedgerAccountType.MemberCredits,
            memberId,
            cancellationToken);
        var history = await ledger.ListAsync(cancellationToken);
        var credits = account is null ? 0 : LedgerBalances.For(account.Id, history);

        return Result<MemberBalance>.Success(
            new MemberBalance(
                memberId,
                credits,
                partner.CreditPolicy.ToMoney(credits, partner.Currency),
                partner.CreditPolicy.DefaultBurnCap));
    }
}
