using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace LoyaltyLab.Resilience.Tests.PaymentSim;

public sealed class PaymentSimTests : IClassFixture<PaymentSimFactory>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly PaymentSimFactory _factory;

    public PaymentSimTests(PaymentSimFactory factory) => _factory = factory;

    [Fact]
    public async Task Same_key_twice_yields_one_authorization()
    {
        using var client = _factory.CreateClient();
        const string key = "saga-1:AuthorizePayment";

        var first = await AuthorizeAsync(client, key, 120.75m);
        var replay = await AuthorizeAsync(client, key, 120.75m);
        var listed = await client.GetFromJsonAsync<JsonElement>("/payments");

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.Body.GetProperty("id").GetGuid().Should().Be(first.Body.GetProperty("id").GetGuid());
        replay.Body.GetProperty("isReplay").GetBoolean().Should().BeTrue();
        listed.EnumerateArray().Count(item => item.GetProperty("authorizeKey").GetString() == key).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_same_key_still_yields_one_authorization()
    {
        using var client = _factory.CreateClient();
        const string key = "saga-concurrent:AuthorizePayment";

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => AuthorizeAsync(client, key, 40.00m))
            .ToArray();
        var responses = await Task.WhenAll(tasks);
        var listed = await client.GetFromJsonAsync<JsonElement>("/payments");

        responses.Select(response => response.Body.GetProperty("id").GetGuid()).Distinct().Should().ContainSingle();
        listed.EnumerateArray().Count(item => item.GetProperty("authorizeKey").GetString() == key).Should().Be(1);
    }

    [Fact]
    public async Task Different_payload_on_the_same_key_is_conflict()
    {
        using var client = _factory.CreateClient();
        const string key = "saga-reuse:AuthorizePayment";

        await AuthorizeAsync(client, key, 10.00m);
        var reused = await AuthorizeAsync(client, key, 11.00m);

        reused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        reused.Body.GetProperty("errorCode").GetString().Should().Be("IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task Query_by_key_returns_the_stored_authorization()
    {
        using var client = _factory.CreateClient();
        const string key = "saga-query:AuthorizePayment";

        var authorized = await AuthorizeAsync(client, key, 25.00m);
        var found = await client.GetAsync($"/payments/by-key?key={Uri.EscapeDataString(key)}");
        var payload = await found.Content.ReadFromJsonAsync<JsonElement>();

        found.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("id").GetGuid().Should().Be(authorized.Body.GetProperty("id").GetGuid());
        payload.GetProperty("status").GetString().Should().Be("Authorized");
    }

    [Fact]
    public async Task Capture_void_and_refund_follow_the_hold_lifecycle()
    {
        using var client = _factory.CreateClient();
        var authorized = await AuthorizeAsync(client, "saga-life:AuthorizePayment", 50.00m);
        var id = authorized.Body.GetProperty("id").GetGuid();

        var captured = await SendAsync(client, HttpMethod.Post, $"/payments/authorizations/{id}/capture", "saga-life:CapturePayment");
        captured.StatusCode.Should().Be(HttpStatusCode.OK);
        captured.Body.GetProperty("status").GetString().Should().Be("Captured");

        var refunded = await SendAsync(client, HttpMethod.Post, $"/payments/authorizations/{id}/refund", "saga-life:RefundPayment");
        refunded.Body.GetProperty("status").GetString().Should().Be("Refunded");

        var other = await AuthorizeAsync(client, "saga-void:AuthorizePayment", 15.00m);
        var voided = await SendAsync(
            client,
            HttpMethod.Post,
            $"/payments/authorizations/{other.Body.GetProperty("id").GetGuid()}/void",
            "saga-void:VoidPayment");
        voided.Body.GetProperty("status").GetString().Should().Be("Voided");
    }

    [Fact]
    public async Task Decline_rate_of_one_always_refuses_authorization()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Simulator:DeclineRate"] = "1",
                });
            });
        });
        using var client = factory.CreateClient();

        var declined = await AuthorizeAsync(client, "saga-decline:AuthorizePayment", 9.00m);

        declined.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        declined.Body.GetProperty("status").GetString().Should().Be("Declined");
    }

    [Fact]
    public async Task Timeout_hangs_after_the_authorization_is_stored()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Simulator:TimeoutRate"] = "1",
                    ["Simulator:TimeoutHangMs"] = "5000",
                });
            });
        });
        using var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromMilliseconds(200);
        const string key = "saga-timeout:AuthorizePayment";

        var send = async () => await AuthorizeAsync(client, key, 18.00m);
        await send.Should().ThrowAsync<TaskCanceledException>();

        using var probe = factory.CreateClient();
        var found = await probe.GetAsync($"/payments/by-key?key={Uri.EscapeDataString(key)}");
        var payload = await found.Content.ReadFromJsonAsync<JsonElement>();

        found.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("status").GetString().Should().Be("Authorized");
    }

    [Fact]
    public async Task Force_decline_header_refuses_authorization()
    {
        using var client = _factory.CreateClient();
        const string key = "saga-force-decline:AuthorizePayment";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/payments/authorizations")
        {
            Content = JsonContent.Create(new { amount = 9.00m, currency = "USD", description = "Coral Bay" }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        request.Headers.TryAddWithoutValidation("X-Sim-Force-Decline", "true");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var found = await client.GetAsync($"/payments/by-key?key={Uri.EscapeDataString(key)}");
        var stored = await found.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        body.GetProperty("status").GetString().Should().Be("Declined");
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        stored.GetProperty("status").GetString().Should().Be("Declined");
    }

    [Fact]
    public async Task Force_timeout_header_stores_then_returns_408()
    {
        using var client = _factory.CreateClient();
        const string key = "saga-force-timeout:AuthorizePayment";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/payments/authorizations")
        {
            Content = JsonContent.Create(new { amount = 18.00m, currency = "USD", description = "Coral Bay" }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        request.Headers.TryAddWithoutValidation("X-Sim-Force-Timeout", "true");

        using var response = await client.SendAsync(request);
        var found = await client.GetAsync($"/payments/by-key?key={Uri.EscapeDataString(key)}");
        var payload = await found.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.RequestTimeout);
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("status").GetString().Should().Be("Authorized");
    }

    [Fact]
    public async Task Force_decline_on_capture_leaves_the_authorization_held()
    {
        using var client = _factory.CreateClient();
        var authorized = await AuthorizeAsync(client, "saga-force-capture:AuthorizePayment", 22.00m);
        var id = authorized.Body.GetProperty("id").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/payments/authorizations/{id}/capture");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "saga-force-capture:CapturePayment");
        request.Headers.TryAddWithoutValidation("X-Sim-Force-Decline", "true");

        using var response = await client.SendAsync(request);
        var listed = await client.GetFromJsonAsync<JsonElement>("/payments");
        var stored = listed.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id);

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        stored.GetProperty("status").GetString().Should().Be("Authorized");
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Body)> AuthorizeAsync(
        HttpClient client,
        string key,
        decimal amount)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/payments/authorizations")
        {
            Content = JsonContent.Create(new { amount, currency = "USD", description = "Coral Bay" }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (response.StatusCode, body);
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Body)> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string key)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (response.StatusCode, body);
    }
}
