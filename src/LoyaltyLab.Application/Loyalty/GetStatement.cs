using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Application.Loyalty;

public sealed class GetStatement(
    ITenantContextAccessor tenant,
    IMemberRepository members,
    ILedgerRepository ledger) : IUseCase<GetStatementQuery, MemberStatement>
{
    public async Task<Result<MemberStatement>> ExecuteAsync(
        GetStatementQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!tenant.Current.HasMember || tenant.Current.MemberId is not { } memberId)
        {
            return Result<MemberStatement>.Failure(Errors.MemberNotFound);
        }

        var member = await members.GetByIdAsync(memberId, cancellationToken);
        if (member is null)
        {
            return Result<MemberStatement>.Failure(Errors.MemberNotFound);
        }

        var account = await ledger.FindAccountAsync(
            tenant.Current.PartnerId,
            LedgerAccountType.MemberCredits,
            memberId,
            cancellationToken);
        if (account is null)
        {
            return Result<MemberStatement>.Success(new MemberStatement(memberId, 0, []));
        }

        var history = await ledger.ListAsync(cancellationToken);
        var lines = new List<StatementLine>();
        var running = 0;
        foreach (var transaction in history)
        {
            var delta = transaction.Entries.Where(entry => entry.AccountId == account.Id).Sum(entry => entry.Amount);
            if (delta == 0)
            {
                continue;
            }

            running += delta;
            lines.Add(
                new StatementLine(
                    transaction.Id,
                    transaction.Type,
                    transaction.OccurredAt,
                    transaction.Reason,
                    delta,
                    running,
                    transaction.ReversesTransactionId));
        }

        return Result<MemberStatement>.Success(new MemberStatement(memberId, running, lines));
    }
}
