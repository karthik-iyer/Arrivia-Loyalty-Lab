using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;

namespace LoyaltyLab.Application.Opportunity;

internal static class InboxNudgeAccess
{
    public static Result<MemberId> RequireMember(ITenantContextAccessor tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (!tenant.Current.HasMember || tenant.Current.MemberId is not { } memberId)
        {
            return Result<MemberId>.Failure(Errors.MemberNotFound);
        }

        return Result<MemberId>.Success(memberId);
    }

    public static async Task<Result<Nudge>> LoadDeliveredAsync(
        INudgeRepository nudges,
        IClock clock,
        IUnitOfWork unitOfWork,
        MemberId memberId,
        NudgeId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nudges);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        var nudge = await nudges.GetByIdAsync(id, cancellationToken);
        if (nudge is null || nudge.MemberId != memberId)
        {
            return Result<Nudge>.Failure(Errors.NudgeNotFound);
        }

        if (nudge.Status == NudgeStatus.Expired || nudge.IsExpired(clock))
        {
            if (nudge.Status == NudgeStatus.Delivered)
            {
                nudge.Expire();
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result<Nudge>.Failure(Errors.NudgeExpired);
        }

        if (nudge.Status != NudgeStatus.Delivered)
        {
            return Result<Nudge>.Failure(Errors.NudgeNotFound);
        }

        return Result<Nudge>.Success(nudge);
    }
}
