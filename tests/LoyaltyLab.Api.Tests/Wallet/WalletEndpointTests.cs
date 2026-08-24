using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Api.Tests.Hosting;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;

namespace LoyaltyLab.Api.Tests.Wallet;

public sealed class WalletEndpointTests : IClassFixture<LoyaltyLabApiFactory>
{
    private readonly LoyaltyLabApiFactory _factory;

    public WalletEndpointTests(LoyaltyLabApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Maya_balance_is_the_seeded_opening_grant()
    {
        using var client = _factory.CreateClient();
        using var request = Wallet("SUMMIT", "/api/wallet/balance", SeedIds.Maya.Value);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("credits").GetInt32().Should().Be(6_000);
        payload.GetProperty("monetaryValue").GetProperty("amount").GetDecimal().Should().Be(60.00m);
        payload.GetProperty("burnCap").GetDecimal().Should().Be(40m);
    }

    [Fact]
    public async Task Statement_includes_the_opening_grant_and_a_running_balance()
    {
        using var client = _factory.CreateClient();
        using var request = Wallet("SUMMIT", "/api/wallet/statement", SeedIds.Maya.Value);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("balance").GetInt32().Should().Be(6_000);
        var line = payload.GetProperty("lines").EnumerateArray().Single();
        line.GetProperty("reason").GetString().Should().Be("Opening grant");
        line.GetProperty("credits").GetInt32().Should().Be(6_000);
        line.GetProperty("runningBalance").GetInt32().Should().Be(6_000);
        line.GetProperty("type").GetString().Should().Be("Earn");
    }

    [Fact]
    public async Task Anonymous_wallet_is_not_found()
    {
        using var client = _factory.CreateClient();
        using var request = Wallet("SUMMIT", "/api/wallet/balance");

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.MemberNotFound.Code);
    }

    [Fact]
    public async Task Member_cannot_read_liability()
    {
        using var client = _factory.CreateClient();
        using var request = Wallet("SUMMIT", "/api/reports/liability?asOf=2026-03-15", SeedIds.Maya.Value);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.RoleNotPermitted.Code);
    }

    [Fact]
    public async Task Finance_reads_summit_outstanding_liability()
    {
        using var client = _factory.CreateClient();
        using var request = Wallet(
            "SUMMIT",
            "/api/reports/liability?asOf=2026-03-15",
            role: AccessRole.FinanceAnalyst);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("creditsIssued").GetInt32().Should().Be(6_500);
        payload.GetProperty("creditsBurned").GetInt32().Should().Be(0);
        payload.GetProperty("creditsExpired").GetInt32().Should().Be(0);
        payload.GetProperty("creditsOutstanding").GetInt32().Should().Be(6_500);
        payload.GetProperty("monetaryLiability").GetProperty("amount").GetDecimal().Should().Be(65.00m);
    }

    [Fact]
    public async Task Summit_finance_does_not_see_nimbus_liability()
    {
        using var client = _factory.CreateClient();
        using var request = Wallet(
            "SUMMIT",
            "/api/reports/liability?asOf=2026-03-15",
            role: AccessRole.FinanceAnalyst);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        payload.GetProperty("creditsOutstanding").GetInt32().Should().Be(6_500);
        payload.GetProperty("creditsOutstanding").GetInt32().Should().NotBe(12_000);
    }

    [Fact]
    public async Task Nimbus_finance_reads_chen_outstanding_liability()
    {
        using var client = _factory.CreateClient();
        using var request = Wallet(
            "NIMBUS",
            "/api/reports/liability?asOf=2026-03-15",
            role: AccessRole.FinanceAnalyst);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("creditsIssued").GetInt32().Should().Be(12_000);
        payload.GetProperty("creditsOutstanding").GetInt32().Should().Be(12_000);
        payload.GetProperty("monetaryLiability").GetProperty("amount").GetDecimal().Should().Be(120.00m);
    }

    private static HttpRequestMessage Wallet(
        string partner,
        string path,
        Guid? memberId = null,
        AccessRole? role = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, partner);
        if (memberId is { } id)
        {
            request.Headers.Add(TenantResolutionMiddleware.MemberHeader, id.ToString());
        }

        if (role is { } access)
        {
            request.Headers.Add(TenantResolutionMiddleware.RoleHeader, access.ToString());
        }

        return request;
    }
}
