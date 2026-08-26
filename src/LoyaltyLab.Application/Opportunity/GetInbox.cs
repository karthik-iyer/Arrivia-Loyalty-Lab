using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;

namespace LoyaltyLab.Application.Opportunity;

/// <summary>
/// Live delivered nudges for the caller. Expired rows are stamped and omitted (FR-O-07).
/// </summary>
public sealed class GetInbox(
    ITenantContextAccessor tenant,
    IClock clock,
    INudgeRepository nudges,
    IUnitOfWork unitOfWork) : IUseCase<GetInboxQuery, GetInboxResult>
{
    public async Task<Result<GetInboxResult>> ExecuteAsync(
        GetInboxQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var member = InboxNudgeAccess.RequireMember(tenant);
        if (member.IsFailure)
        {
            return Result<GetInboxResult>.Failure(member.Error);
        }

        var rows = await nudges.ListForMemberAsync(member.Value, cancellationToken);
        var expired = false;
        var visible = new List<InboxNudge>();
        foreach (var row in rows.OrderByDescending(nudge => nudge.CreatedAt))
        {
            if (row.Status != NudgeStatus.Delivered)
            {
                continue;
            }

            if (row.IsExpired(clock))
            {
                var tracked = await nudges.GetByIdAsync(row.Id, cancellationToken);
                if (tracked is { Status: NudgeStatus.Delivered })
                {
                    tracked.Expire();
                    expired = true;
                }

                continue;
            }

            visible.Add(InboxNudge.From(row));
        }

        if (expired)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<GetInboxResult>.Success(new GetInboxResult(visible));
    }
}
