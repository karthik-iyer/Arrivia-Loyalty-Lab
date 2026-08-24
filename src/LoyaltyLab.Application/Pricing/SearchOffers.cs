using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;

namespace LoyaltyLab.Application.Pricing;

public sealed class SearchOffers(
    ITenantContextAccessor tenant,
    IOfferRepository offers,
    IPricingRuleRepository rules,
    IPartnerSupplierRepository permits,
    IClock clock) : IUseCase<SearchOffersQuery, IReadOnlyList<OfferSummary>>
{
    public async Task<Result<IReadOnlyList<OfferSummary>>> ExecuteAsync(
        SearchOffersQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = tenant.Current;
        var asOf = clock.UtcNow;
        var permitted = await permits.GetPermittedSupplierIdsAsync(context.PartnerId, cancellationToken);
        var catalog = await offers.ListAsync(cancellationToken);
        var partnerRules = await rules.ListForPartnerAsync(context.PartnerId, asOf, cancellationToken);

        var items = new List<OfferSummary>();
        foreach (var offer in catalog)
        {
            if (request.StayDate < offer.AvailableFrom || request.StayDate > offer.AvailableTo)
            {
                continue;
            }

            if (!permitted.Contains(offer.SupplierId))
            {
                continue;
            }

            Money? memberPrice = null;
            if (context.HasMember)
            {
                var priced = OfferPricing.Run(
                    context.PartnerId,
                    offer,
                    context.Tier,
                    request.StayDate,
                    permitted,
                    partnerRules,
                    asOf);

                if (priced.IsRejected)
                {
                    continue;
                }

                memberPrice = priced.RunningTotal;
            }

            items.Add(
                new OfferSummary(
                    offer.Id,
                    offer.PropertyName,
                    offer.Destination.Code,
                    offer.Destination.DisplayName,
                    offer.StarRating,
                    offer.Tags,
                    offer.AvailableFrom,
                    offer.AvailableTo,
                    memberPrice));
        }

        return Result<IReadOnlyList<OfferSummary>>.Success(items);
    }
}
