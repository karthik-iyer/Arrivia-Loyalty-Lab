using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;

namespace LoyaltyLab.Api.Tests.Hosting;

public sealed class TenantHeaderTests : IClassFixture<LoyaltyLabApiFactory>
{
    private readonly LoyaltyLabApiFactory _factory;

    public TenantHeaderTests(LoyaltyLabApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Missing_partner_header_returns_PARTNER_NOT_RESOLVED()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/partners/current/theme");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.PartnerNotResolved.Code);
        payload.GetProperty("title").GetString().Should().Be(Errors.PartnerNotResolved.Message);
        response.Headers.Contains(CorrelationIdMiddleware.HeaderName).Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_partner_code_returns_PARTNER_NOT_RESOLVED()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/partners/current/theme");
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, "UNKNOWN");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.PartnerNotResolved.Code);
    }

    [Fact]
    public async Task Health_does_not_require_a_partner()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Theme_uses_the_resolved_partner()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/partners/current/theme");
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, "SUMMIT");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("code").GetString().Should().Be("SUMMIT");
        payload.GetProperty("displayName").GetString().Should().Be("Summit Rewards");
    }

    [Fact]
    public async Task Development_host_uses_the_fixed_demo_clock()
    {
        _factory.Services.GetRequiredService<IClock>().Should().BeOfType<FixedDemoClock>();
        _factory.Services.GetRequiredService<IClock>().UtcNow
            .Should().Be(new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));
    }
}
