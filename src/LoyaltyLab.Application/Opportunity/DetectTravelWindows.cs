using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;

namespace LoyaltyLab.Application.Opportunity;

public sealed class DetectTravelWindows(
    ITenantContextAccessor tenant,
    IClock clock,
    IMemberRepository members,
    IPartnerRepository partners,
    IBusyPeriodRepository busyPeriods) : IUseCase<DetectTravelWindowsQuery, DetectTravelWindowsResult>
{
    public async Task<Result<DetectTravelWindowsResult>> ExecuteAsync(
        DetectTravelWindowsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!tenant.Current.HasMember || tenant.Current.MemberId is not { } memberId)
        {
            return Result<DetectTravelWindowsResult>.Failure(Errors.MemberNotFound);
        }

        var member = await members.GetByIdAsync(memberId, cancellationToken);
        var partner = await partners.GetByIdAsync(tenant.Current.PartnerId, cancellationToken);
        if (member is null || partner is null)
        {
            return Result<DetectTravelWindowsResult>.Failure(Errors.MemberNotFound);
        }

        var busy = await busyPeriods.ListForMemberAsync(memberId, cancellationToken);
        var windows = WindowDetector.Detect(member.Id, busy, partner.OpportunityPolicy, clock);
        return Result<DetectTravelWindowsResult>.Success(new DetectTravelWindowsResult(windows));
    }
}
