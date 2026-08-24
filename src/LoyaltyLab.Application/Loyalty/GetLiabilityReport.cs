using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Loyalty;

public sealed class GetLiabilityReport(
    ITenantContextAccessor tenant,
    IPartnerRepository partners,
    ILedgerRepository ledger) : IUseCase<GetLiabilityReportQuery, LiabilityReport>
{
    public async Task<Result<LiabilityReport>> ExecuteAsync(
        GetLiabilityReportQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var partner = await partners.GetByIdAsync(tenant.Current.PartnerId, cancellationToken);
        if (partner is null)
        {
            return Result<LiabilityReport>.Failure(Errors.PartnerNotResolved);
        }

        if (tenant.Current.Role != AccessRole.FinanceAnalyst)
        {
            return Result<LiabilityReport>.Failure(Errors.RoleNotPermitted);
        }

        var asOf = LedgerBalances.OnOrBefore(await ledger.ListAsync(cancellationToken), request.AsOf);
        var accounts = await ledger.ListAccountsAsync(cancellationToken);

        var issued = -Balance(accounts, LedgerAccountType.PartnerIssuance, asOf);
        var burned = Balance(accounts, LedgerAccountType.PartnerRedemption, asOf);
        var expired = Balance(accounts, LedgerAccountType.PartnerBreakage, asOf);
        var outstanding = accounts
            .Where(account => account.Type == LedgerAccountType.MemberCredits)
            .Sum(account => LedgerBalances.For(account.Id, asOf));

        return Result<LiabilityReport>.Success(
            new LiabilityReport(
                partner.Id,
                request.AsOf,
                issued,
                burned,
                expired,
                outstanding,
                partner.CreditPolicy.ToMoney(outstanding, partner.Currency)));
    }

    private static int Balance(
        IReadOnlyList<LedgerAccount> accounts,
        LedgerAccountType type,
        IReadOnlyList<LedgerTransaction> history)
    {
        var account = accounts.SingleOrDefault(item => item.Type == type);
        return account is null ? 0 : LedgerBalances.For(account.Id, history);
    }
}
