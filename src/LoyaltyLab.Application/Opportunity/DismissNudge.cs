using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;

namespace LoyaltyLab.Application.Opportunity;

/// <summary>
/// Marks a live nudge dismissed so fatigue cooldown can see it on the next scan (FR-O-10).
/// </summary>
public sealed class DismissNudge(
    ITenantContextAccessor tenant,
    IClock clock,
    INudgeRepository nudges,
    IUnitOfWork unitOfWork) : IUseCase<DismissNudgeCommand, DismissNudgeResult>
{
    public async Task<Result<DismissNudgeResult>> ExecuteAsync(
        DismissNudgeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var member = InboxNudgeAccess.RequireMember(tenant);
        if (member.IsFailure)
        {
            return Result<DismissNudgeResult>.Failure(member.Error);
        }

        var loaded = await InboxNudgeAccess.LoadDeliveredAsync(
            nudges,
            clock,
            unitOfWork,
            member.Value,
            request.NudgeId,
            cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<DismissNudgeResult>.Failure(loaded.Error);
        }

        var nudge = loaded.Value;
        nudge.Dismiss();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<DismissNudgeResult>.Success(new DismissNudgeResult(nudge.Id, nudge.Status));
    }
}
