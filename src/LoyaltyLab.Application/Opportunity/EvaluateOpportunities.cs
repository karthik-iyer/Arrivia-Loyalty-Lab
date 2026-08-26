using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Application.Pricing;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Opportunity;

/// <summary>
/// Detect windows, price eligible inventory through the normal engine, score, and persist (FR-O-01, FR-O-02, FR-O-04).
/// Does not persist a quote — actioning re-quotes through QuoteOffer (FR-O-09). Fatigue (FR-O-06) runs before a deliver.
/// </summary>
public sealed class EvaluateOpportunities(
    ITenantContextAccessor tenant,
    IClock clock,
    IMemberRepository members,
    IPartnerRepository partners,
    IBusyPeriodRepository busyPeriods,
    IOfferRepository offers,
    IPartnerSupplierRepository permits,
    IPricingRuleRepository rules,
    IBookingRepository bookings,
    IQuoteRepository quotes,
    IPriceWatchRepository watches,
    INudgeRepository nudges,
    GetBalance getBalance,
    IUnitOfWork unitOfWork) : IUseCase<EvaluateOpportunitiesCommand, EvaluateOpportunitiesResult>
{
    public async Task<Result<EvaluateOpportunitiesResult>> ExecuteAsync(
        EvaluateOpportunitiesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!tenant.Current.HasMember || tenant.Current.MemberId is not { } memberId)
        {
            return Result<EvaluateOpportunitiesResult>.Failure(Errors.MemberNotFound);
        }

        var member = await members.GetByIdAsync(memberId, cancellationToken);
        var partner = await partners.GetByIdAsync(tenant.Current.PartnerId, cancellationToken);
        if (member is null || partner is null)
        {
            return Result<EvaluateOpportunitiesResult>.Failure(Errors.MemberNotFound);
        }

        var policy = partner.OpportunityPolicy;
        var busy = await busyPeriods.ListForMemberAsync(memberId, cancellationToken);
        var windows = WindowDetector.Detect(member.Id, busy, policy, clock);
        var recorded = new List<Nudge>();

        if (windows.Count == 0)
        {
            var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
            var placeholder = new TravelWindow(member.Id, today, today.AddDays(policy.MinWindowNights));
            recorded.Add(Nudge.Suppress(
                partner.Id,
                member.Id,
                placeholder,
                SuppressionReason.WindowTooSoon,
                policy,
                clock));
        }
        else
        {
            var catalog = await offers.ListAsync(cancellationToken);
            var permitted = await permits.GetPermittedSupplierIdsAsync(partner.Id, cancellationToken);
            var partnerRules = await rules.ListForPartnerAsync(partner.Id, clock.UtcNow, cancellationToken);
            var balance = await getBalance.ExecuteAsync(new GetBalanceQuery(), cancellationToken);
            if (balance.IsFailure)
            {
                return Result<EvaluateOpportunitiesResult>.Failure(balance.Error);
            }

            var history = await LoadHistoryAsync(memberId, catalog, cancellationToken);
            var watchByOffer = (await watches.ListAsync(cancellationToken))
                .ToDictionary(watch => watch.OfferId);
            var prior = (await nudges.ListForMemberAsync(memberId, cancellationToken)).ToList();

            foreach (var window in windows)
            {
                var nudge = EvaluateWindow(
                    member,
                    partner,
                    window,
                    catalog,
                    permitted,
                    partnerRules,
                    history,
                    watchByOffer,
                    balance.Value.MonetaryValue,
                    prior);
                recorded.Add(nudge);
                prior.Add(nudge);
            }
        }

        foreach (var nudge in recorded)
        {
            await nudges.AddAsync(nudge, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EvaluateOpportunitiesResult>.Success(
            new EvaluateOpportunitiesResult(recorded.Select(EvaluatedNudge.From).ToArray()));
    }

    private Nudge EvaluateWindow(
        Member member,
        Partner partner,
        TravelWindow window,
        IReadOnlyList<TravelOffer> catalog,
        IReadOnlySet<SupplierId> permitted,
        IReadOnlyList<PricingRule> partnerRules,
        IReadOnlyList<CompletedStay> history,
        Dictionary<OfferId, PriceWatch> watchByOffer,
        Money creditBalance,
        IReadOnlyList<Nudge> prior)
    {
        var policy = partner.OpportunityPolicy;
        var asOf = clock.UtcNow;
        ScoredCandidate? best = null;

        foreach (var offer in catalog)
        {
            var priced = OfferPricing.Run(
                partner.Id,
                offer,
                member.Tier,
                window.Start,
                permitted,
                partnerRules,
                asOf);
            if (priced.IsRejected || priced.MaxCreditTender is not { } tender)
            {
                continue;
            }

            watchByOffer.TryGetValue(offer.Id, out var watch);
            var signals = OpportunityScorer.Score(
                window,
                offer,
                policy,
                history,
                priced.RunningTotal,
                tender,
                creditBalance,
                watch?.BaselineNetRate);
            var score = OpportunityScorer.Total(signals);
            var candidate = new ScoredCandidate(offer.Id, score, signals);
            if (best is null
                || candidate.Score > best.Score
                || (candidate.Score == best.Score && candidate.OfferId.Value.CompareTo(best.OfferId.Value) < 0))
            {
                best = candidate;
            }
        }

        if (best is null)
        {
            return Nudge.Suppress(
                partner.Id,
                member.Id,
                window,
                SuppressionReason.NoEligibleInventory,
                policy,
                clock);
        }

        if (best.Score < policy.ScoreThreshold)
        {
            return Nudge.Suppress(
                partner.Id,
                member.Id,
                window,
                SuppressionReason.ScoreBelowThreshold,
                policy,
                clock,
                best.OfferId,
                best.Signals);
        }

        var fatigue = FatigueRules.FirstMatch(best.OfferId, window, prior, policy, clock);
        if (fatigue is { } reason)
        {
            return Nudge.Suppress(
                partner.Id,
                member.Id,
                window,
                reason,
                policy,
                clock,
                best.OfferId,
                best.Signals);
        }

        return Nudge.Deliver(
            partner.Id,
            member.Id,
            best.OfferId,
            window,
            best.Signals,
            policy,
            clock);
    }

    private async Task<IReadOnlyList<CompletedStay>> LoadHistoryAsync(
        MemberId memberId,
        IReadOnlyList<TravelOffer> catalog,
        CancellationToken cancellationToken)
    {
        var byOffer = catalog.ToDictionary(offer => offer.Id);
        var stays = new List<CompletedStay>();
        var history = await bookings.ListForMemberAsync(memberId, cancellationToken);
        foreach (var booking in history.Where(item => item.Status == BookingStatus.Confirmed))
        {
            var quote = await quotes.GetByIdAsync(booking.QuoteId, cancellationToken);
            if (quote is null || !byOffer.TryGetValue(quote.OfferId, out var offer))
            {
                continue;
            }

            stays.Add(new CompletedStay(offer.Destination, offer.Tags));
        }

        return stays;
    }

    private sealed record ScoredCandidate(
        OfferId OfferId,
        decimal Score,
        IReadOnlyList<OpportunitySignal> Signals);
}
