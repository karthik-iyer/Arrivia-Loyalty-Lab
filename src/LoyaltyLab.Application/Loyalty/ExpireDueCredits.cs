using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Loyalty;

/// <summary>
/// Partner-wide FIFO expiry worker. Posts one explicit expire per member with due lots (FR-L-09).
/// </summary>
public sealed class ExpireDueCredits(
    ITenantContextAccessor tenant,
    IPartnerRepository partners,
    ILedgerRepository ledger,
    ExpireCredits expire,
    IClock clock) : IUseCase<ExpireDueCreditsCommand, ExpireDueCreditsResult>
{
    public async Task<Result<ExpireDueCreditsResult>> ExecuteAsync(
        ExpireDueCreditsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var partner = await partners.GetByIdAsync(tenant.Current.PartnerId, cancellationToken);
        if (partner is null)
        {
            return Result<ExpireDueCreditsResult>.Failure(Errors.PartnerNotResolved);
        }

        var now = clock.UtcNow;
        var asOfDate = DateOnly.FromDateTime(now.UtcDateTime);
        var history = await ledger.ListAsync(cancellationToken);
        var dueAccounts = (await ledger.ListAccountsAsync(cancellationToken))
            .Where(account => account.Type == LedgerAccountType.MemberCredits)
            .ToList();
        var posted = new List<LedgerPostingResult>();

        foreach (var memberCredits in dueAccounts)
        {
            if (memberCredits.MemberId is not { } memberId)
            {
                continue;
            }

            var due = CreditLots.Due(
                CreditLots.Remaining(history, memberCredits.Id, partner.CreditPolicy.CreditLifetimeDays),
                now);
            if (due <= 0)
            {
                continue;
            }

            var result = await expire.ExecuteAsync(
                new ExpireCreditsCommand(
                    memberId,
                    due,
                    $"due:{memberId.Value:N}:{asOfDate:yyyy-MM-dd}",
                    "Credit lifetime elapsed"),
                cancellationToken);
            if (result.IsFailure)
            {
                return Result<ExpireDueCreditsResult>.Failure(result.Error);
            }

            posted.Add(result.Value);
            history = await ledger.ListAsync(cancellationToken);
        }

        return Result<ExpireDueCreditsResult>.Success(new ExpireDueCreditsResult(posted));
    }
}
