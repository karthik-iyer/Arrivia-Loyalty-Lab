using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Api.Tests.Hosting;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoyaltyLab.Api.Tests.Opportunity;

public sealed class InboxEndpointTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Maya_inbox_lists_the_scanned_coral_nudge_with_signals()
    {
        using var factory = new LoyaltyLabApiFactory();
        using var client = factory.CreateClient();
        await ScanAsync(client);

        using var request = Member("SUMMIT", HttpMethod.Get, "/api/inbox", SeedIds.Maya.Value);
        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: payload.ToString());
        var nudge = payload.GetProperty("nudges").EnumerateArray().Should().ContainSingle().Subject;
        nudge.GetProperty("offerId").GetGuid().Should().Be(SeedIds.Offer(1).Value);
        nudge.GetProperty("propertyName").GetString().Should().Be("Coral Bay Resort");
        nudge.GetProperty("windowStart").GetString().Should().Be("2026-03-29");
        nudge.GetProperty("windowEnd").GetString().Should().Be("2026-04-12");
        nudge.GetProperty("score").GetDecimal().Should().BeGreaterThanOrEqualTo(0.55m);
        nudge.GetProperty("signals").EnumerateArray().Should().HaveCount(5);
        ContainsNetRate(payload.GetRawText()).Should().BeFalse();
    }

    [Fact]
    public async Task Actioning_returns_a_fresh_engine_quote()
    {
        using var factory = new LoyaltyLabApiFactory();
        using var client = factory.CreateClient();
        var nudgeId = await ScanAndInboxIdAsync(client);

        using var action = Member("SUMMIT", HttpMethod.Post, $"/api/inbox/{nudgeId}/action", SeedIds.Maya.Value);
        var response = await client.SendAsync(action);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: payload.ToString());
        payload.GetProperty("nudgeId").GetGuid().Should().Be(nudgeId);
        payload.GetProperty("offerId").GetGuid().Should().Be(SeedIds.Offer(1).Value);
        payload.GetProperty("memberPrice").GetProperty("amount").GetDecimal().Should().Be(120.75m);
        payload.GetProperty("maxCredits").GetInt32().Should().Be(4830);
        ContainsNetRate(payload.GetRawText()).Should().BeFalse();

        using var inbox = Member("SUMMIT", HttpMethod.Get, "/api/inbox", SeedIds.Maya.Value);
        var listed = await client.SendAsync(inbox);
        var remaining = await listed.Content.ReadFromJsonAsync<JsonElement>(Json);
        remaining.GetProperty("nudges").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Dismissing_removes_the_nudge_from_the_inbox()
    {
        using var factory = new LoyaltyLabApiFactory();
        using var client = factory.CreateClient();
        var nudgeId = await ScanAndInboxIdAsync(client);

        using var dismiss = Member("SUMMIT", HttpMethod.Post, $"/api/inbox/{nudgeId}/dismiss", SeedIds.Maya.Value);
        var response = await client.SendAsync(dismiss);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: payload.ToString());
        payload.GetProperty("status").GetString().Should().Be("Dismissed");

        using var inbox = Member("SUMMIT", HttpMethod.Get, "/api/inbox", SeedIds.Maya.Value);
        var listed = await client.SendAsync(inbox);
        var remaining = await listed.Content.ReadFromJsonAsync<JsonElement>(Json);
        remaining.GetProperty("nudges").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Actioning_after_the_configured_lifetime_is_gone()
    {
        using var factory = new AdvancingClockApiFactory();
        using var client = factory.CreateClient();
        var nudgeId = await ScanAndInboxIdAsync(client);

        factory.Clock.UtcNow = factory.Clock.UtcNow.AddDays(7);

        using var inbox = Member("SUMMIT", HttpMethod.Get, "/api/inbox", SeedIds.Maya.Value);
        var listed = await client.SendAsync(inbox);
        var remaining = await listed.Content.ReadFromJsonAsync<JsonElement>(Json);
        listed.StatusCode.Should().Be(HttpStatusCode.OK, because: remaining.ToString());
        remaining.GetProperty("nudges").EnumerateArray().Should().BeEmpty();

        using var action = Member("SUMMIT", HttpMethod.Post, $"/api/inbox/{nudgeId}/action", SeedIds.Maya.Value);
        var response = await client.SendAsync(action);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.NudgeExpired.Code);
    }

    [Fact]
    public async Task Cross_tenant_action_is_not_found()
    {
        using var factory = new LoyaltyLabApiFactory();
        using var client = factory.CreateClient();
        var nudgeId = await ScanAndInboxIdAsync(client);

        using var action = Member("NIMBUS", HttpMethod.Post, $"/api/inbox/{nudgeId}/action", SeedIds.Chen.Value);
        var response = await client.SendAsync(action);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.NudgeNotFound.Code);
    }

    [Fact]
    public async Task Another_summit_member_cannot_action_mayas_nudge()
    {
        using var factory = new LoyaltyLabApiFactory();
        using var client = factory.CreateClient();
        var nudgeId = await ScanAndInboxIdAsync(client);

        using var action = Member("SUMMIT", HttpMethod.Post, $"/api/inbox/{nudgeId}/action", SeedIds.Ravi.Value);
        var response = await client.SendAsync(action);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.NudgeNotFound.Code);
    }

    [Fact]
    public async Task Anonymous_inbox_is_not_found()
    {
        using var factory = new LoyaltyLabApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/inbox");
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, "SUMMIT");

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.MemberNotFound.Code);
    }

    private static async Task<Guid> ScanAndInboxIdAsync(HttpClient client)
    {
        await ScanAsync(client);
        using var inbox = Member("SUMMIT", HttpMethod.Get, "/api/inbox", SeedIds.Maya.Value);
        var listed = await client.SendAsync(inbox);
        var payload = await listed.Content.ReadFromJsonAsync<JsonElement>(Json);
        listed.StatusCode.Should().Be(HttpStatusCode.OK, because: payload.ToString());
        return payload.GetProperty("nudges").EnumerateArray().Single().GetProperty("nudgeId").GetGuid();
    }

    private static async Task ScanAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/run/scan");
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, "SUMMIT");
        request.Headers.Add(TenantResolutionMiddleware.RoleHeader, AccessRole.Operator.ToString());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: body);
    }

    private static HttpRequestMessage Member(string partner, HttpMethod method, string path, Guid memberId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, partner);
        request.Headers.Add(TenantResolutionMiddleware.MemberHeader, memberId.ToString());
        return request;
    }

    private static bool ContainsNetRate(string json) =>
        json.Contains("netRate", StringComparison.OrdinalIgnoreCase);
}

public sealed class AdvancingClockApiFactory : LoyaltyLabApiFactory
{
    public MutableTestClock Clock { get; } = new(
        new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(entry => entry.ServiceType == typeof(IClock)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IClock>(Clock);
        });
    }
}

public sealed class MutableTestClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}
