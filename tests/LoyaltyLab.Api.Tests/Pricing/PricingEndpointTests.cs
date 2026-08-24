using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Api.Tests.Hosting;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Infrastructure.Persistence;

namespace LoyaltyLab.Api.Tests.Pricing;

public sealed class PricingEndpointTests : IClassFixture<LoyaltyLabApiFactory>
{
    private static readonly DateOnly Stay = new(2026, 3, 15);

    private readonly LoyaltyLabApiFactory _factory;

    public PricingEndpointTests(LoyaltyLabApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_offer_search_json_contains_no_netRate()
    {
        using var client = _factory.CreateClient();
        using var request = OfferSearch("SUMMIT");

        var response = await client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ContainsNetRate(raw).Should().BeFalse("anonymous payloads must omit netRate entirely (FR-X-05)");
        raw.Should().Contain("Coral Bay Resort");
        raw.Should().NotContain("memberPrice");
    }

    [Fact]
    public async Task Member_search_includes_the_worked_example_price()
    {
        using var client = _factory.CreateClient();
        using var request = OfferSearch("SUMMIT", SeedIds.Maya.Value);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var coral = payload.EnumerateArray().Single(o => o.GetProperty("propertyName").GetString() == "Coral Bay Resort");
        coral.GetProperty("memberPrice").GetProperty("amount").GetDecimal().Should().Be(120.75m);
        ContainsNetRate(payload.GetRawText()).Should().BeFalse();
    }

    [Fact]
    public async Task Nimbus_cannot_see_oceanic_inventory()
    {
        using var client = _factory.CreateClient();
        using var request = OfferSearch("NIMBUS");

        var response = await client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        raw.Should().NotContain("Coral Bay Resort");
        ContainsNetRate(raw).Should().BeFalse();
    }

    [Fact]
    public async Task Quote_returns_the_summit_gold_worked_example()
    {
        using var client = _factory.CreateClient();
        using var request = Quote("SUMMIT", SeedIds.Maya.Value, SeedIds.Offer(1).Value);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("memberPrice").GetProperty("amount").GetDecimal().Should().Be(120.75m);
        payload.GetProperty("maxCreditTender").GetProperty("amount").GetDecimal().Should().Be(48.30m);
        payload.GetProperty("maxCredits").GetInt32().Should().Be(4830);
        ContainsNetRate(payload.GetRawText()).Should().BeFalse();
    }

    [Fact]
    public async Task Two_partners_price_the_same_alpine_offer_differently()
    {
        using var client = _factory.CreateClient();
        var alpine = SeedIds.Offer(9).Value;

        using var summitRequest = Quote("SUMMIT", SeedIds.Maya.Value, alpine);
        using var nimbusRequest = Quote("NIMBUS", SeedIds.Chen.Value, alpine);
        var summit = await client.SendAsync(summitRequest);
        var nimbus = await client.SendAsync(nimbusRequest);
        var summitJson = await summit.Content.ReadAsStringAsync();
        var nimbusJson = await nimbus.Content.ReadAsStringAsync();
        var summitPrice = JsonDocument.Parse(summitJson).RootElement.GetProperty("memberPrice").GetProperty("amount").GetDecimal();
        var nimbusPrice = JsonDocument.Parse(nimbusJson).RootElement.GetProperty("memberPrice").GetProperty("amount").GetDecimal();

        summit.StatusCode.Should().Be(HttpStatusCode.OK);
        nimbus.StatusCode.Should().Be(HttpStatusCode.OK);
        summitPrice.Should().Be(219.45m);
        nimbusPrice.Should().Be(238.36m);
        summitPrice.Should().NotBe(nimbusPrice);
        ContainsNetRate(summitJson).Should().BeFalse();
        ContainsNetRate(nimbusJson).Should().BeFalse();
    }

    [Fact]
    public async Task Quote_of_oceanic_for_nimbus_is_not_eligible()
    {
        using var client = _factory.CreateClient();
        using var request = Quote("NIMBUS", SeedIds.Chen.Value, SeedIds.Offer(1).Value);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be((HttpStatusCode)422);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.OfferNotEligible.Code);
    }

    [Fact]
    public async Task Anonymous_quote_does_not_disclose_the_offer()
    {
        using var client = _factory.CreateClient();
        using var request = Quote("SUMMIT", memberId: null, SeedIds.Offer(1).Value);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.OfferNotFound.Code);
    }

    [Fact]
    public async Task Explain_for_the_quoting_member_omits_netRate()
    {
        using var client = _factory.CreateClient();
        using var quoteRequest = Quote("SUMMIT", SeedIds.Maya.Value, SeedIds.Offer(1).Value);
        var quoted = await client.SendAsync(quoteRequest);
        var quote = await quoted.Content.ReadFromJsonAsync<JsonElement>();
        var quoteId = quote.GetProperty("quoteId").GetGuid();

        using var explainRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/quotes/{quoteId}/explain");
        explainRequest.Headers.Add(TenantResolutionMiddleware.PartnerHeader, "SUMMIT");
        explainRequest.Headers.Add(TenantResolutionMiddleware.MemberHeader, SeedIds.Maya.Value.ToString());

        var response = await client.SendAsync(explainRequest);
        var raw = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ContainsNetRate(raw).Should().BeFalse();
        raw.Should().NotContain("netCost");
        raw.Should().Contain("120.75");
    }

    [Fact]
    public async Task Explain_under_another_partner_is_not_found()
    {
        using var client = _factory.CreateClient();
        using var quoteRequest = Quote("SUMMIT", SeedIds.Maya.Value, SeedIds.Offer(1).Value);
        var quoted = await client.SendAsync(quoteRequest);
        var quote = await quoted.Content.ReadFromJsonAsync<JsonElement>();
        var quoteId = quote.GetProperty("quoteId").GetGuid();

        using var explainRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/quotes/{quoteId}/explain");
        explainRequest.Headers.Add(TenantResolutionMiddleware.PartnerHeader, "NIMBUS");
        explainRequest.Headers.Add(TenantResolutionMiddleware.MemberHeader, SeedIds.Chen.Value.ToString());

        var response = await client.SendAsync(explainRequest);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.QuoteNotFound.Code);
    }

    private static HttpRequestMessage OfferSearch(string partner, Guid? memberId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/offers?stayDate={Stay:yyyy-MM-dd}");
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, partner);
        if (memberId is { } id)
        {
            request.Headers.Add(TenantResolutionMiddleware.MemberHeader, id.ToString());
        }

        return request;
    }

    private static HttpRequestMessage Quote(string partner, Guid? memberId, Guid offerId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/offers/{offerId}/quote")
        {
            Content = JsonContent.Create(new { stayDate = Stay }),
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
