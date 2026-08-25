using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoyaltyLab.Api.Endpoints;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Application.Concierge;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Application.Pricing;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;

namespace LoyaltyLab.Api.Http;

internal sealed class McpUseCases(
    TenantBinder tenant,
    Recommend recommend,
    ExplainQuote explain,
    GetBalance getBalance) : IMcpUseCases
{
    private static readonly JsonSerializerOptions Json = CreateJson();

    public async Task<string> GetTravelRecommendationsAsync(
        string partnerCode,
        Guid memberId,
        string text,
        DateOnly? stayDate,
        string? destinationCode,
        decimal? maxBudget,
        string? accessRole,
        CancellationToken cancellationToken)
    {
        var bound = await tenant.BindAsync(partnerCode, memberId.ToString(), accessRole, cancellationToken);
        if (bound is not null)
        {
            return WriteError(bound);
        }

        var result = await recommend.ExecuteAsync(
            new RecommendCommand(text, stayDate, destinationCode, maxBudget),
            cancellationToken);

        return result.Match(payload => Write(ConciergeRecommendHttp.From(payload)), WriteError);
    }

    public async Task<string> ExplainOfferPriceAsync(
        string partnerCode,
        Guid memberId,
        Guid quoteId,
        string? accessRole,
        CancellationToken cancellationToken)
    {
        var bound = await tenant.BindAsync(partnerCode, memberId.ToString(), accessRole, cancellationToken);
        if (bound is not null)
        {
            return WriteError(bound);
        }

        var result = await explain.ExecuteAsync(new ExplainQuoteQuery(new QuoteId(quoteId)), cancellationToken);
        return result.Match(payload => Write(ExplainHttp.From(payload)), WriteError);
    }

    public async Task<string> GetCreditBalanceAsync(
        string partnerCode,
        Guid memberId,
        string? accessRole,
        CancellationToken cancellationToken)
    {
        var bound = await tenant.BindAsync(partnerCode, memberId.ToString(), accessRole, cancellationToken);
        if (bound is not null)
        {
            return WriteError(bound);
        }

        var result = await getBalance.ExecuteAsync(new GetBalanceQuery(), cancellationToken);
        return result.Match(payload => Write(WalletBalanceHttp.From(payload)), WriteError);
    }

    private static string Write<T>(T payload) => JsonSerializer.Serialize(payload, Json);

    private static string WriteError(Error error) => Write(new McpError(error.Code, error.Message));

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record McpError(string ErrorCode, string Title);
}
