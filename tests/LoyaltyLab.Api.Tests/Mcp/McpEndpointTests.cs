using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Api.Tests.Hosting;
using LoyaltyLab.Infrastructure.Persistence;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LoyaltyLab.Api.Tests.Mcp;

public sealed class McpEndpointTests : IClassFixture<LoyaltyLabApiFactory>
{
    private readonly LoyaltyLabApiFactory _factory;

    public McpEndpointTests(LoyaltyLabApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Agent_discovers_the_three_concierge_tools()
    {
        using var http = _factory.CreateClient();
        await using var mcp = await ConnectAsync(http);

        var names = (await mcp.ListToolsAsync()).Select(tool => tool.Name).ToArray();

        names.Should().Contain("get_travel_recommendations");
        names.Should().Contain("explain_offer_price");
        names.Should().Contain("get_credit_balance");
    }

    [Fact]
    public async Task Balance_tool_matches_the_wallet_endpoint()
    {
        using var http = _factory.CreateClient();
        using var restRequest = Get("SUMMIT", "/api/wallet/balance", SeedIds.Maya.Value);
        var restResponse = await http.SendAsync(restRequest);
        var rest = await restResponse.Content.ReadFromJsonAsync<JsonElement>();

        await using var mcp = await ConnectAsync(http);
        var tool = await CallJsonAsync(
            mcp,
            "get_credit_balance",
            new Dictionary<string, object?>
            {
                ["partnerCode"] = "SUMMIT",
                ["memberId"] = SeedIds.Maya.Value,
            });

        restResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        tool.GetProperty("memberId").GetGuid().Should().Be(rest.GetProperty("memberId").GetGuid());
        tool.GetProperty("credits").GetInt32().Should().Be(rest.GetProperty("credits").GetInt32());
        tool.GetProperty("monetaryValue").GetProperty("amount").GetDecimal()
            .Should().Be(rest.GetProperty("monetaryValue").GetProperty("amount").GetDecimal());
        tool.GetProperty("burnCap").GetDecimal().Should().Be(rest.GetProperty("burnCap").GetDecimal());
    }

    [Fact]
    public async Task Recommend_tool_matches_rest_for_the_same_request()
    {
        using var http = _factory.CreateClient();
        using var restRequest = PostRecommend("SUMMIT", SeedIds.Maya.Value, "beach in Montego Bay in March");
        var restResponse = await http.SendAsync(restRequest);
        var rest = await restResponse.Content.ReadFromJsonAsync<JsonElement>();

        await using var mcp = await ConnectAsync(http);
        var tool = await CallJsonAsync(
            mcp,
            "get_travel_recommendations",
            new Dictionary<string, object?>
            {
                ["partnerCode"] = "SUMMIT",
                ["memberId"] = SeedIds.Maya.Value,
                ["text"] = "beach in Montego Bay in March",
            });

        restResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        tool.GetProperty("narrative").GetString().Should().Be(rest.GetProperty("narrative").GetString());
        tool.GetProperty("narrationApplied").GetBoolean().Should().Be(rest.GetProperty("narrationApplied").GetBoolean());
        ComparableRecommendations(tool).Should().BeEquivalentTo(ComparableRecommendations(rest));
        AuditSnapshot(tool).Should().BeEquivalentTo(AuditSnapshot(rest));
    }

    [Fact]
    public async Task Explain_tool_matches_rest_for_the_same_quote()
    {
        using var http = _factory.CreateClient();
        await using var mcp = await ConnectAsync(http);
        var recommended = await CallJsonAsync(
            mcp,
            "get_travel_recommendations",
            new Dictionary<string, object?>
            {
                ["partnerCode"] = "SUMMIT",
                ["memberId"] = SeedIds.Maya.Value,
                ["text"] = "beach in Montego Bay in March",
            });
        var quoteId = recommended.GetProperty("recommendations")
            .EnumerateArray()
            .Single(item => item.GetProperty("propertyName").GetString() == "Coral Bay Resort")
            .GetProperty("quoteId")
            .GetGuid();

        using var restRequest = Get("SUMMIT", $"/api/quotes/{quoteId}/explain", SeedIds.Maya.Value);
        var restResponse = await http.SendAsync(restRequest);
        var rest = await restResponse.Content.ReadFromJsonAsync<JsonElement>();

        var tool = await CallJsonAsync(
            mcp,
            "explain_offer_price",
            new Dictionary<string, object?>
            {
                ["partnerCode"] = "SUMMIT",
                ["memberId"] = SeedIds.Maya.Value,
                ["quoteId"] = quoteId,
            });

        restResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        rest.GetRawText().Should().NotContain("netRate", "member explain payloads omit netRate");
        tool.GetRawText().Should().Be(rest.GetRawText());
    }

    private static async Task<McpClient> ConnectAsync(HttpClient http)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, "mcp"),
            },
            http);

        return await McpClient.CreateAsync(transport);
    }

    private static async Task<JsonElement> CallJsonAsync(
        McpClient client,
        string tool,
        Dictionary<string, object?> arguments)
    {
        var result = await client.CallToolAsync(tool, arguments);
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        result.IsError.GetValueOrDefault().Should().BeFalse("tool {0} failed: {1}", tool, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static HttpRequestMessage Get(string partner, string path, Guid memberId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, partner);
        request.Headers.Add(TenantResolutionMiddleware.MemberHeader, memberId.ToString());
        return request;
    }

    private static HttpRequestMessage PostRecommend(string partner, Guid memberId, string text)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/concierge/recommend")
        {
            Content = JsonContent.Create(new { text }),
        };
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, partner);
        request.Headers.Add(TenantResolutionMiddleware.MemberHeader, memberId.ToString());
        return request;
    }

    private static ComparableStay[] ComparableRecommendations(JsonElement payload) =>
        [.. payload.GetProperty("recommendations")
            .EnumerateArray()
            .Select(item => new ComparableStay(
                item.GetProperty("offerId").GetGuid(),
                item.GetProperty("propertyName").GetString(),
                item.GetProperty("memberPrice").GetProperty("amount").GetDecimal(),
                item.GetProperty("creditsCover").GetInt32(),
                item.GetProperty("score").GetDecimal(),
                [.. item.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString())]))];

    private static ComparableAudit AuditSnapshot(JsonElement payload)
    {
        var audit = payload.GetProperty("audit");
        return new ComparableAudit(
            audit.GetProperty("candidatesConsidered").GetInt32(),
            audit.GetProperty("candidatesReturned").GetInt32(),
            [.. audit.GetProperty("interpretedTerms").EnumerateArray().Select(term => term.GetString())],
            [.. audit.GetProperty("exclusions")
                .EnumerateArray()
                .Select(item => new ComparableExclusion(
                    item.GetProperty("offerId").GetGuid(),
                    item.GetProperty("reason").GetString(),
                    item.GetProperty("detail").GetString()))],
            audit.GetProperty("weights").GetRawText(),
            audit.GetProperty("narrationApplied").GetBoolean());
    }

    private sealed record ComparableStay(
        Guid OfferId,
        string? PropertyName,
        decimal MemberPrice,
        int CreditsCover,
        decimal Score,
        string?[] Reasons);

    private sealed record ComparableExclusion(Guid OfferId, string? Reason, string? Detail);

    private sealed record ComparableAudit(
        int CandidatesConsidered,
        int CandidatesReturned,
        string?[] InterpretedTerms,
        ComparableExclusion[] Exclusions,
        string Weights,
        bool NarrationApplied);
}
