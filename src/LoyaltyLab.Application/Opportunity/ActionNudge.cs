using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Pricing;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;

namespace LoyaltyLab.Application.Opportunity;

/// <summary>
/// Re-quotes through the normal engine so a nudge never carries a stale price into checkout (FR-O-09).
/// </summary>
public sealed class ActionNudge(
    ITenantContextAccessor tenant,
    IClock clock,
    INudgeRepository nudges,
    QuoteOffer quote,
    IUnitOfWork unitOfWork) : IUseCase<ActionNudgeCommand, ActionNudgeResult>
{
    public async Task<Result<ActionNudgeResult>> ExecuteAsync(
        ActionNudgeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var member = InboxNudgeAccess.RequireMember(tenant);
        if (member.IsFailure)
        {
            return Result<ActionNudgeResult>.Failure(member.Error);
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
            return Result<ActionNudgeResult>.Failure(loaded.Error);
        }

        var nudge = loaded.Value;
        if (nudge.OfferId is not { } offerId)
        {
            throw new DomainException("A delivered nudge must name an offer.");
        }

        var quoted = await quote.ExecuteAsync(
            new QuoteOfferCommand(offerId, nudge.WindowStart),
            cancellationToken);
        if (quoted.IsFailure)
        {
            return Result<ActionNudgeResult>.Failure(quoted.Error);
        }

        nudge.Action();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ActionNudgeResult>.Success(
            new ActionNudgeResult(
                nudge.Id,
                quoted.Value.QuoteId,
                quoted.Value.OfferId,
                quoted.Value.MemberPrice,
                quoted.Value.MaxCreditTender,
                quoted.Value.MaxCredits,
                quoted.Value.ExpiresAt));
    }
}
