namespace LoyaltyLab.Api.Http;

/// <summary>
/// MCP tool surface. Implementations live outside <c>Mcp/</c> so adapters stay forwarding-only (ADR-0010).
/// </summary>
public interface IMcpUseCases
{
    Task<string> GetTravelRecommendationsAsync(
        string partnerCode,
        Guid memberId,
        string text,
        DateOnly? stayDate,
        string? destinationCode,
        decimal? maxBudget,
        string? accessRole,
        CancellationToken cancellationToken);

    Task<string> ExplainOfferPriceAsync(
        string partnerCode,
        Guid memberId,
        Guid quoteId,
        string? accessRole,
        CancellationToken cancellationToken);

    Task<string> GetCreditBalanceAsync(
        string partnerCode,
        Guid memberId,
        string? accessRole,
        CancellationToken cancellationToken);
}
