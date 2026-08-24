using LoyaltyLab.Api.Http;
using LoyaltyLab.Application.Pricing;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;

namespace LoyaltyLab.Api.Endpoints;

internal static class PricingEndpoints
{
    public static void MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/offers", SearchAsync);
        app.MapPost("/api/offers/{offerId:guid}/quote", QuoteAsync);
        app.MapGet("/api/quotes/{quoteId:guid}/explain", ExplainAsync);
    }

    private static async Task<IResult> SearchAsync(
        DateOnly? stayDate,
        SearchOffers search,
        IClock clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var stay = stayDate ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var result = await search.ExecuteAsync(new SearchOffersQuery(stay), cancellationToken);
        return result.Match(
            offers => Results.Ok(offers.Select(OfferHttp.From).ToArray()),
            error => ProblemResults.FromError(http, error));
    }

    private static async Task<IResult> QuoteAsync(
        Guid offerId,
        QuoteOfferRequest? body,
        QuoteOffer quote,
        IClock clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var stay = body?.StayDate ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var result = await quote.ExecuteAsync(new QuoteOfferCommand(new OfferId(offerId), stay), cancellationToken);
        return result.Match(
            quoted => Results.Ok(QuoteHttp.From(quoted)),
            error => ProblemResults.FromError(http, error));
    }

    private static async Task<IResult> ExplainAsync(
        Guid quoteId,
        ExplainQuote explain,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await explain.ExecuteAsync(new ExplainQuoteQuery(new QuoteId(quoteId)), cancellationToken);
        return result.Match(
            explanation => Results.Ok(ExplainHttp.From(explanation)),
            error => ProblemResults.FromError(http, error));
    }
}

internal sealed record QuoteOfferRequest(DateOnly? StayDate);

internal sealed record MoneyHttp(decimal Amount, string Currency)
{
    public static MoneyHttp From(Money money) => new(money.Amount, money.Currency.Code);

    public static MoneyHttp? From(Money? money) => money is { } value ? From(value) : null;
}

internal sealed record OfferHttp(
    Guid OfferId,
    string PropertyName,
    string DestinationCode,
    string DestinationName,
    int StarRating,
    IReadOnlyCollection<OfferTag> Tags,
    DateOnly AvailableFrom,
    DateOnly AvailableTo,
    MoneyHttp? MemberPrice)
{
    public static OfferHttp From(OfferSummary offer) =>
        new(
            offer.OfferId.Value,
            offer.PropertyName,
            offer.DestinationCode,
            offer.DestinationName,
            offer.StarRating,
            offer.Tags,
            offer.AvailableFrom,
            offer.AvailableTo,
            MoneyHttp.From(offer.MemberPrice));
}

internal sealed record QuoteHttp(
    Guid QuoteId,
    Guid OfferId,
    MoneyHttp MemberPrice,
    MoneyHttp MaxCreditTender,
    int MaxCredits,
    DateTimeOffset ExpiresAt)
{
    public static QuoteHttp From(QuoteResult quote) =>
        new(
            quote.QuoteId.Value,
            quote.OfferId.Value,
            MoneyHttp.From(quote.MemberPrice),
            MoneyHttp.From(quote.MaxCreditTender),
            quote.MaxCredits,
            quote.ExpiresAt);
}

internal sealed record TraceHttp(
    PricingStageKind Stage,
    int Order,
    string Description,
    Guid? AppliedRule,
    MoneyHttp SubtotalBefore,
    MoneyHttp SubtotalAfter,
    bool WasClamped,
    string? ClampReason)
{
    public static TraceHttp From(PriceTraceEntry entry) =>
        new(
            entry.Stage,
            entry.Order,
            entry.Description,
            entry.AppliedRule?.Value,
            MoneyHttp.From(entry.SubtotalBefore),
            MoneyHttp.From(entry.SubtotalAfter),
            entry.WasClamped,
            entry.ClampReason);
}

internal sealed record ExplainHttp(
    IReadOnlyList<TraceHttp> Stages,
    MoneyHttp MemberPrice,
    MoneyHttp? MaxCreditTender,
    MoneyHttp? NetCost,
    MoneyHttp? Margin)
{
    public static ExplainHttp From(PriceExplanation explanation) =>
        new(
            explanation.Stages.Select(TraceHttp.From).ToArray(),
            MoneyHttp.From(explanation.MemberPrice),
            MoneyHttp.From(explanation.MaxCreditTender),
            MoneyHttp.From(explanation.NetCost),
            MoneyHttp.From(explanation.Margin));
}
