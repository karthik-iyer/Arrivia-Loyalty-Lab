using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Api.Tests.Hosting;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Infrastructure.Persistence;

namespace LoyaltyLab.Api.Tests.Concierge;

public sealed class ConciergeEndpointTests : IClassFixture<LoyaltyLabApiFactory>
{
    private readonly LoyaltyLabApiFactory _factory;

    public ConciergeEndpointTests(LoyaltyLabApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Maya_beach_in_montego_returns_coral_with_a_live_quote()
    {
        using var client = _factory.CreateClient();
        using var request = Recommend("SUMMIT", SeedIds.Maya.Value, "beach in Montego Bay in March");

        var response = await client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();
        var payload = JsonDocument.Parse(raw).RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ContainsNetRate(raw).Should().BeFalse("member payloads must omit netRate entirely (FR-X-05)");
        payload.GetProperty("narrationApplied").GetBoolean().Should().BeFalse();
        payload.GetProperty("narrative").GetString().Should().NotBeNullOrWhiteSpace();
        payload.GetProperty("narrative").GetString().Should().NotContain("$", because: "the template must not invent amounts");

        var coral = payload.GetProperty("recommendations")
            .EnumerateArray()
            .Single(item => item.GetProperty("propertyName").GetString() == "Coral Bay Resort");
        coral.GetProperty("offerId").GetGuid().Should().Be(SeedIds.Offer(1).Value);
        coral.GetProperty("memberPrice").GetProperty("amount").GetDecimal().Should().Be(120.75m);
        coral.GetProperty("creditsCover").GetInt32().Should().Be(4830);
        var quoteId = coral.GetProperty("quoteId").GetGuid();
        quoteId.Should().NotBe(Guid.Empty);

        var terms = payload.GetProperty("audit").GetProperty("interpretedTerms")
            .EnumerateArray()
            .Select(term => term.GetString())
            .ToArray();
        terms.Should().Contain("beach");
        terms.Should().Contain("March");
        terms.Should().Contain("Montego Bay");
        payload.GetProperty("audit").GetProperty("candidatesConsidered").GetInt32().Should().Be(24);
        payload.GetProperty("audit").GetProperty("candidatesReturned").GetInt32()
            .Should().Be(payload.GetProperty("recommendations").GetArrayLength());
        payload.GetProperty("audit").GetProperty("exclusions").EnumerateArray()
            .Should().OnlyContain(exclusion =>
                !string.IsNullOrWhiteSpace(exclusion.GetProperty("reason").GetString())
                && !string.IsNullOrWhiteSpace(exclusion.GetProperty("detail").GetString()));
        payload.GetProperty("audit").GetProperty("weights").GetProperty("valueForMoney").GetDecimal().Should().Be(0.40m);

        using var explain = new HttpRequestMessage(HttpMethod.Get, $"/api/quotes/{quoteId}/explain");
        explain.Headers.Add(TenantResolutionMiddleware.PartnerHeader, "SUMMIT");
        explain.Headers.Add(TenantResolutionMiddleware.MemberHeader, SeedIds.Maya.Value.ToString());
        var explained = await client.SendAsync(explain);
        var explainJson = await explained.Content.ReadFromJsonAsync<JsonElement>();

        explained.StatusCode.Should().Be(HttpStatusCode.OK);
        explainJson.GetProperty("memberPrice").GetProperty("amount").GetDecimal().Should().Be(120.75m);
        ContainsNetRate(explainJson.GetRawText()).Should().BeFalse();
    }

    [Fact]
    public async Task Nimbus_does_not_recommend_oceanic_inventory()
    {
        using var client = _factory.CreateClient();
        using var request = Recommend("NIMBUS", SeedIds.Chen.Value, "beach in Montego Bay in March");

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("recommendations").EnumerateArray()
            .Select(item => item.GetProperty("propertyName").GetString())
            .Should()
            .NotContain("Coral Bay Resort");
        payload.GetProperty("audit").GetProperty("exclusions").EnumerateArray()
            .Should()
            .Contain(exclusion =>
                exclusion.GetProperty("offerId").GetGuid() == SeedIds.Offer(1).Value
                && exclusion.GetProperty("reason").GetString() == "SupplierNotPermitted");
        ContainsNetRate(payload.GetRawText()).Should().BeFalse();
    }

    [Fact]
    public async Task Anonymous_recommend_is_not_found()
    {
        using var client = _factory.CreateClient();
        using var request = Recommend("SUMMIT", memberId: null, "beach");

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.MemberNotFound.Code);
    }

    private static HttpRequestMessage Recommend(string partner, Guid? memberId, string text)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/concierge/recommend")
        {
            Content = JsonContent.Create(new { text }),
        };
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, partner);
        if (memberId is { } id)
        {
            request.Headers.Add(TenantResolutionMiddleware.MemberHeader, id.ToString());
        }

        return request;
    }

    private static bool ContainsNetRate(string json) =>
        json.Contains("netRate", StringComparison.OrdinalIgnoreCase);
}
