using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Resilience.Tests.PaymentSim;

namespace LoyaltyLab.Resilience.Tests.Booking;

public sealed class SagaChaosTests : IClassFixture<PaymentSimFactory>
{
    private static readonly DateOnly Stay = new(2026, 3, 15);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly PaymentSimFactory _payments;

    public SagaChaosTests(PaymentSimFactory payments) => _payments = payments;

    [Fact]
    public async Task Payment_decline_releases_the_reservation()
    {
        using var api = CreateApi();
        using var client = api.CreateClient();
        var booked = await BookMayaAsync(client, "chaos-decline", credits: 0, """{"paymentDecline":true}""");

        booked.StatusCode.Should().Be(HttpStatusCode.OK, because: booked.Body.ToString());
        booked.Body.GetProperty("saga").GetProperty("status").GetString().Should().Be("Compensated");
        var sagaId = booked.Body.GetProperty("saga").GetProperty("id").GetGuid();
        var reserved = await OperatorStepAsync(client, sagaId, "ReserveInventory");

        reserved.GetProperty("status").GetString().Should().Be("Compensated");
        reserved.GetProperty("compensation").GetProperty("status").GetString().Should().Be("Succeeded");
        (await PaymentsForAsync(sagaId)).Should().ContainSingle()
            .Which.GetProperty("status").GetString().Should().Be("Declined");
    }

    [Fact]
    public async Task Capture_failure_reverses_the_burn()
    {
        using var api = CreateApi();
        using var client = api.CreateClient();
        var booked = await BookMayaAsync(client, "chaos-capture", credits: 4830, """{"paymentCaptureDecline":true}""");

        booked.StatusCode.Should().Be(HttpStatusCode.OK, because: booked.Body.ToString());
        booked.Body.GetProperty("saga").GetProperty("status").GetString().Should().Be("Compensated");
        var sagaId = booked.Body.GetProperty("saga").GetProperty("id").GetGuid();
        var burned = await OperatorStepAsync(client, sagaId, "BurnCredits");
        using var wallet = Member("SUMMIT", HttpMethod.Get, "/api/wallet/balance", SeedIds.Maya.Value);
        var balance = await client.SendAsync(wallet);
        var walletBody = await balance.Content.ReadFromJsonAsync<JsonElement>(Json);

        burned.GetProperty("status").GetString().Should().Be("Compensated");
        burned.GetProperty("compensation").GetProperty("status").GetString().Should().Be("Succeeded");
        balance.StatusCode.Should().Be(HttpStatusCode.OK);
        walletBody.GetProperty("credits").GetInt32().Should().Be(6_000);
        (await PaymentsForAsync(sagaId)).Should().ContainSingle()
            .Which.GetProperty("status").GetString().Should().Be("Voided");
    }

    [Fact]
    public async Task Payment_timeout_resolves_by_query_to_a_single_authorization()
    {
        using var api = CreateApi();
        using var client = api.CreateClient();
        var parked = await BookMayaAsync(client, "chaos-timeout", credits: 0, """{"paymentTimeout":true}""");

        parked.StatusCode.Should().Be(HttpStatusCode.OK, because: parked.Body.ToString());
        parked.Body.GetProperty("saga").GetProperty("status").GetString().Should().Be("Running");
        parked.Body.GetProperty("saga").GetProperty("steps").EnumerateArray()
            .Single(step => step.GetProperty("kind").GetString() == "AuthorizePayment")
            .GetProperty("status").GetString().Should().Be("Unknown");

        var resumed = await BookMayaAsync(client, "chaos-timeout", credits: 0, quoteId: parked.QuoteId);
        resumed.StatusCode.Should().Be(HttpStatusCode.OK, because: resumed.Body.ToString());
        var sagaId = resumed.Body.GetProperty("saga").GetProperty("id").GetGuid();

        resumed.StatusCode.Should().Be(HttpStatusCode.OK, because: resumed.Body.ToString());
        resumed.Body.GetProperty("saga").GetProperty("status").GetString().Should().Be("Confirmed");
        (await PaymentsForAsync(sagaId)).Should().ContainSingle()
            .Which.GetProperty("status").GetString().Should().Be("Captured");
    }

    [Fact]
    public async Task Kill_and_recovery_leave_exactly_one_authorization()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"loyaltylab-chaos-kill-{Guid.NewGuid():N}.db");
        Guid sagaId;
        using (var api = new ChaosApiFactory(_payments, dbPath, deleteDatabase: false))
        {
            using var client = api.CreateClient();
            var crashed = await BookMayaAsync(
                client,
                "chaos-kill",
                credits: 0,
                """{"crashAfterStep":"AuthorizePayment"}""");
            crashed.StatusCode.Should().Be(HttpStatusCode.InternalServerError, because: crashed.Body.ToString());
            sagaId = await RunningSagaIdAsync(client);
            (await PaymentsForAsync(sagaId)).Should().ContainSingle()
                .Which.GetProperty("status").GetString().Should().Be("Authorized");
        }

        using (var restarted = new ChaosApiFactory(_payments, dbPath, clock: "2026-03-15T12:02:00+00:00"))
        {
            using var client = restarted.CreateClient();
            using var recover = Role("SUMMIT", HttpMethod.Post, "/api/admin/run/recovery", AccessRole.Operator);
            var recovered = await client.SendAsync(recover);
            var payload = await recovered.Content.ReadFromJsonAsync<JsonElement>(Json);
            var saga = await OperatorSagaAsync(client, sagaId);

            recovered.StatusCode.Should().Be(HttpStatusCode.OK, because: payload.ToString());
            payload.GetProperty("processed").GetInt32().Should().Be(1);
            saga.GetProperty("status").GetString().Should().Be("Confirmed");
            (await PaymentsForAsync(sagaId)).Should().ContainSingle()
                .Which.GetProperty("status").GetString().Should().Be("Captured");
        }
    }

    [Fact]
    public async Task Exhausted_compensation_lands_in_manual_review()
    {
        using var api = CreateApi();
        using var client = api.CreateClient();
        var booked = await BookMayaAsync(
            client,
            "chaos-review",
            credits: 0,
            """{"paymentDecline":true,"supplierReleaseFail":true}""");

        booked.StatusCode.Should().Be(HttpStatusCode.OK, because: booked.Body.ToString());
        booked.Body.GetProperty("saga").GetProperty("status").GetString().Should().Be("RequiresManualReview");
        var sagaId = booked.Body.GetProperty("saga").GetProperty("id").GetGuid();
        var reserved = await OperatorStepAsync(client, sagaId, "ReserveInventory");

        reserved.GetProperty("status").GetString().Should().Be("CompensationFailed");
        reserved.GetProperty("compensation").GetProperty("status").GetString().Should().Be("Failed");
        reserved.GetProperty("compensation").GetProperty("attempts").GetInt32().Should().Be(5);
    }

    private ChaosApiFactory CreateApi() =>
        new(_payments, Path.Combine(Path.GetTempPath(), $"loyaltylab-chaos-{Guid.NewGuid():N}.db"));

    private async Task<JsonElement[]> PaymentsForAsync(Guid sagaId)
    {
        using var client = _payments.CreateClient();
        var listed = await client.GetFromJsonAsync<JsonElement>("/payments");
        var key = SagaInstance.DeriveIdempotencyKey(new SagaInstanceId(sagaId), SagaStepKind.AuthorizePayment);
        return [.. listed.EnumerateArray().Where(item => item.GetProperty("authorizeKey").GetString() == key)];
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Body, Guid QuoteId)> BookMayaAsync(
        HttpClient client,
        string key,
        int credits,
        string? faultProfile = null,
        Guid? quoteId = null)
    {
        var id = quoteId ?? Guid.Empty;
        if (quoteId is null)
        {
            using var quoteRequest = Member("SUMMIT", HttpMethod.Post, $"/api/offers/{SeedIds.Offer(1).Value}/quote", SeedIds.Maya.Value);
            quoteRequest.Content = JsonContent.Create(new { stayDate = Stay });
            using var quoted = await client.SendAsync(quoteRequest);
            var quote = await quoted.Content.ReadFromJsonAsync<JsonElement>(Json);
            quoted.StatusCode.Should().Be(HttpStatusCode.OK, because: quote.ToString());
            id = quote.GetProperty("quoteId").GetGuid();
        }

        using var book = Member("SUMMIT", HttpMethod.Post, "/api/bookings", SeedIds.Maya.Value);
        book.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        if (faultProfile is not null)
        {
            book.Headers.TryAddWithoutValidation(FaultInjectionMiddleware.HeaderName, faultProfile);
        }

        book.Content = JsonContent.Create(new { quoteId = id, credits, stayDate = Stay });
        using var response = await client.SendAsync(book);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (response.StatusCode, body, id);
    }

    private static async Task<JsonElement> OperatorSagaAsync(HttpClient client, Guid sagaId)
    {
        using var request = Role("SUMMIT", HttpMethod.Get, $"/api/operator/sagas/{sagaId}", AccessRole.Operator);
        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: payload.ToString());
        return payload;
    }

    private static async Task<JsonElement> OperatorStepAsync(HttpClient client, Guid sagaId, string kind)
    {
        var saga = await OperatorSagaAsync(client, sagaId);
        return saga.GetProperty("steps").EnumerateArray()
            .Single(step => step.GetProperty("kind").GetString() == kind);
    }

    private static async Task<Guid> RunningSagaIdAsync(HttpClient client)
    {
        using var request = Role("SUMMIT", HttpMethod.Get, "/api/operator/sagas", AccessRole.Operator);
        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: payload.ToString());
        return payload.EnumerateArray().Single(item => item.GetProperty("status").GetString() == "Running")
            .GetProperty("id").GetGuid();
    }

    private static HttpRequestMessage Member(string partner, HttpMethod method, string path, Guid memberId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, partner);
        request.Headers.Add(TenantResolutionMiddleware.MemberHeader, memberId.ToString());
        return request;
    }

    private static HttpRequestMessage Role(string partner, HttpMethod method, string path, AccessRole role)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, partner);
        request.Headers.Add(TenantResolutionMiddleware.RoleHeader, role.ToString());
        return request;
    }
}
