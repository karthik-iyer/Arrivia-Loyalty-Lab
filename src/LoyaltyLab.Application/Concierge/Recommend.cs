using System.Collections.Frozen;
using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Application.Pricing;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Concierge;
using LoyaltyLab.Domain.Pricing;

namespace LoyaltyLab.Application.Concierge;

/// <summary>
/// Parse → quote every catalog row through the pricing engine → filter → rank → narrate (FR-C-01 … FR-C-07).
/// </summary>
public sealed class Recommend(
    ITenantContextAccessor tenant,
    IClock clock,
    IOfferRepository offers,
    IPartnerSupplierRepository permits,
    QuoteOffer quote,
    GetBalance getBalance,
    IOfferNarrator narrator) : IUseCase<RecommendCommand, RecommendResult>
{
    public async Task<Result<RecommendResult>> ExecuteAsync(
        RecommendCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var balance = await getBalance.ExecuteAsync(new GetBalanceQuery(), cancellationToken);
        if (balance.IsFailure)
        {
            return Result<RecommendResult>.Failure(balance.Error);
        }

        var catalog = await offers.ListAsync(cancellationToken);
        var permitted = await permits.GetPermittedSupplierIdsAsync(tenant.Current.PartnerId, cancellationToken);
        var calendarAnchor = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var parsed = CriteriaParser.Parse(
            request.Text,
            DestinationLexicon.For(catalog.Select(offer => offer.Destination)),
            calendarAnchor,
            Overlay(request, balance.Value.MonetaryValue.Currency));

        var stay = parsed.Criteria.StayDate ?? calendarAnchor;
        var criteria = parsed.Criteria with { StayDate = stay };

        var quotes = new Dictionary<OfferId, PricedCandidate>();
        var quoteIds = new Dictionary<OfferId, QuoteId>();
        foreach (var offer in catalog)
        {
            var priced = await quote.ExecuteAsync(new QuoteOfferCommand(offer.Id, stay), cancellationToken);
            if (priced.IsFailure)
            {
                continue;
            }

            quotes[offer.Id] = new PricedCandidate(
                offer,
                priced.Value.MemberPrice,
                priced.Value.MaxCreditTender,
                priced.Value.MaxCredits);
            quoteIds[offer.Id] = priced.Value.QuoteId;
        }

        var ranked = CandidatePipeline.Evaluate(
            new RecommendationRequest(
                criteria,
                parsed.InterpretedTerms,
                catalog,
                permitted,
                quotes,
                balance.Value.Credits));

        var spoken = await GroundedNarration.ApplyAsync(narrator, ranked, cancellationToken);
        return Result<RecommendResult>.Success(
            new RecommendResult(
                spoken.Narrative,
                spoken.Audit.NarrationApplied,
                BindQuotes(ranked.Recommendations, quoteIds),
                spoken.Audit));
    }

    private static RecommendationCriteria? Overlay(RecommendCommand request, Currency currency)
    {
        var destination = string.IsNullOrWhiteSpace(request.DestinationCode)
            ? null
            : request.DestinationCode.Trim();
        Money? budget = request.MaxBudget is > 0m
            ? Money.Of(request.MaxBudget.Value, currency)
            : null;

        if (request.StayDate is null && destination is null && budget is null)
        {
            return null;
        }

        return new RecommendationCriteria(
            destination,
            FrozenSet<OfferTag>.Empty,
            request.StayDate,
            budget);
    }

    private static List<RecommendedOffer> BindQuotes(
        IReadOnlyList<RankedRecommendation> ranked,
        Dictionary<OfferId, QuoteId> quoteIds)
    {
        var items = new List<RecommendedOffer>(ranked.Count);
        foreach (var item in ranked)
        {
            if (!quoteIds.TryGetValue(item.OfferId, out var quoteId))
            {
                throw new InvalidOperationException(
                    $"Ranked offer {item.OfferId.Value:D} has no persisted quote.");
            }

            items.Add(
                new RecommendedOffer(
                    item.OfferId,
                    item.PropertyName,
                    quoteId,
                    item.MemberPrice,
                    item.CreditsCover,
                    item.Score,
                    item.Reasons));
        }

        return items;
    }
}
