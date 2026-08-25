using System.ComponentModel;
using LoyaltyLab.Api.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace LoyaltyLab.Api.Mcp;

/// <summary>
/// Thin MCP adapters. Each call opens a scope and forwards to <see cref="IMcpUseCases"/> (ADR-0010).
/// </summary>
[McpServerToolType]
public sealed class ConciergeTools
{
    [McpServerTool(Name = "get_travel_recommendations"), Description("Ranked eligible stays with live quotes and an audit block.")]
    public static Task<string> GetTravelRecommendations(
        [Description("Partner code such as SUMMIT or NIMBUS.")] string partnerCode,
        [Description("Member identifier.")] Guid memberId,
        [Description("Natural-language request, optionally with dates, destination, or budget.")] string text,
        IServiceScopeFactory scopes,
        CancellationToken cancellationToken,
        [Description("Optional stay date overlay (yyyy-MM-dd).")] DateOnly? stayDate = null,
        [Description("Optional destination code overlay such as MBJ.")] string? destinationCode = null,
        [Description("Optional maximum member price.")] decimal? maxBudget = null,
        [Description("Optional access role, matching X-Access-Role.")] string? accessRole = null) =>
        Invoke(scopes, useCases => useCases.GetTravelRecommendationsAsync(
            partnerCode, memberId, text, stayDate, destinationCode, maxBudget, accessRole, cancellationToken));

    [McpServerTool(Name = "explain_offer_price"), Description("Role-filtered price trace for a previously issued quote.")]
    public static Task<string> ExplainOfferPrice(
        [Description("Partner code such as SUMMIT or NIMBUS.")] string partnerCode,
        [Description("Member identifier.")] Guid memberId,
        [Description("Quote identifier returned by get_travel_recommendations or POST /offers/{id}/quote.")] Guid quoteId,
        IServiceScopeFactory scopes,
        CancellationToken cancellationToken,
        [Description("Optional access role, matching X-Access-Role.")] string? accessRole = null) =>
        Invoke(scopes, useCases => useCases.ExplainOfferPriceAsync(
            partnerCode, memberId, quoteId, accessRole, cancellationToken));

    [McpServerTool(Name = "get_credit_balance"), Description("Member credit balance, monetary equivalent, and burn cap.")]
    public static Task<string> GetCreditBalance(
        [Description("Partner code such as SUMMIT or NIMBUS.")] string partnerCode,
        [Description("Member identifier.")] Guid memberId,
        IServiceScopeFactory scopes,
        CancellationToken cancellationToken,
        [Description("Optional access role, matching X-Access-Role.")] string? accessRole = null) =>
        Invoke(scopes, useCases => useCases.GetCreditBalanceAsync(
            partnerCode, memberId, accessRole, cancellationToken));

    private static async Task<string> Invoke(IServiceScopeFactory scopes, Func<IMcpUseCases, Task<string>> call)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<IMcpUseCases>());
    }
}
