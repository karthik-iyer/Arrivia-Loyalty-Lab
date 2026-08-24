using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Application.Pricing;

public sealed record SearchOffersQuery(DateOnly StayDate);

public sealed record OfferSummary(
    OfferId OfferId,
    string PropertyName,
    string DestinationCode,
    string DestinationName,
    int StarRating,
    IReadOnlyCollection<OfferTag> Tags,
    DateOnly AvailableFrom,
    DateOnly AvailableTo,
    Money? MemberPrice);

public sealed record QuoteOfferCommand(OfferId OfferId, DateOnly StayDate);

public sealed record QuoteResult(
    QuoteId QuoteId,
    OfferId OfferId,
    Money MemberPrice,
    Money MaxCreditTender,
    int MaxCredits,
    DateTimeOffset ExpiresAt);

public sealed record ExplainQuoteQuery(QuoteId QuoteId);
