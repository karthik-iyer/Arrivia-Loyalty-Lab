using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Api.Tests.Hosting;
using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Booking;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoyaltyLab.Api.Tests.Booking;

public sealed class BookingEndpointTests : IClassFixture<BookingApiFactory>
{
    private static readonly DateOnly Stay = new(2026, 3, 15);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly BookingApiFactory _factory;

    public BookingEndpointTests(BookingApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Checkout_returns_a_confirmed_saga_with_every_step()
    {
        using var client = _factory.CreateClient();
        var booked = await BookMayaAsync(client, "idem-checkout-1", credits: 4830);

        booked.StatusCode.Should().Be(HttpStatusCode.OK, because: booked.Body.ToString());
        booked.Body.GetProperty("status").GetString().Should().Be("Confirmed");
        booked.Body.GetProperty("tender").GetProperty("credits").GetInt32().Should().Be(4830);
        var steps = booked.Body.GetProperty("saga").GetProperty("steps").EnumerateArray().ToArray();
        steps.Should().HaveCount(6);
        steps.Should().OnlyContain(step => step.GetProperty("status").GetString() == "Succeeded");
        steps.Should().Contain(step => step.GetProperty("attempts").GetInt32() >= 1);
    }

    [Fact]
    public async Task Get_booking_matches_the_checkout_payload()
    {
        using var client = _factory.CreateClient();
        var booked = await BookMayaAsync(client, "idem-get-1");
        booked.StatusCode.Should().Be(HttpStatusCode.OK, because: booked.Body.ToString());
        var bookingId = booked.Body.GetProperty("bookingId").GetGuid();

        using var request = Member("SUMMIT", HttpMethod.Get, $"/api/bookings/{bookingId}", SeedIds.Maya.Value);
        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("bookingId").GetGuid().Should().Be(bookingId);
        payload.GetProperty("saga").GetProperty("status").GetString().Should().Be("Confirmed");
    }

    [Fact]
    public async Task Operator_payload_includes_steps_attempts_and_compensation_slots()
    {
        using var client = _factory.CreateClient();
        var booked = await BookMayaAsync(client, "idem-operator-1");
        booked.StatusCode.Should().Be(HttpStatusCode.OK, because: booked.Body.ToString());
        var sagaId = booked.Body.GetProperty("saga").GetProperty("id").GetGuid();

        using var request = Role("SUMMIT", HttpMethod.Get, $"/api/operator/sagas/{sagaId}", AccessRole.Operator);
        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("id").GetGuid().Should().Be(sagaId);
        payload.GetProperty("poison").GetArrayLength().Should().Be(0);
        var steps = payload.GetProperty("steps").EnumerateArray().ToArray();
        steps.Should().HaveCount(6);
        steps.Should().OnlyContain(step => step.GetProperty("attempts").GetInt32() >= 1);
        foreach (var step in steps)
        {
            step.TryGetProperty("compensation", out _).Should().BeFalse();
        }
    }

    [Fact]
    public async Task Member_cannot_read_operator_sagas()
    {
        using var client = _factory.CreateClient();
        using var request = Member("SUMMIT", HttpMethod.Get, "/api/operator/sagas", SeedIds.Maya.Value);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.RoleNotPermitted.Code);
    }

    [Fact]
    public async Task Cross_tenant_booking_is_not_found()
    {
        using var client = _factory.CreateClient();
        var booked = await BookMayaAsync(client, "idem-cross-1");
        booked.StatusCode.Should().Be(HttpStatusCode.OK, because: booked.Body.ToString());
        var bookingId = booked.Body.GetProperty("bookingId").GetGuid();

        using var request = Member("NIMBUS", HttpMethod.Get, $"/api/bookings/{bookingId}", SeedIds.Chen.Value);
        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.BookingNotFound.Code);
    }

    [Fact]
    public async Task Cancel_requires_an_idempotency_key()
    {
        using var client = _factory.CreateClient();
        var booked = await BookMayaAsync(client, "idem-cancel-missing");
        booked.StatusCode.Should().Be(HttpStatusCode.OK, because: booked.Body.ToString());
        var bookingId = booked.Body.GetProperty("bookingId").GetGuid();

        using var request = Member("SUMMIT", HttpMethod.Post, $"/api/bookings/{bookingId}/cancel", SeedIds.Maya.Value);
        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.MissingIdempotencyKey.Code);
    }

    [Fact]
    public async Task Operator_can_run_the_outbox_worker()
    {
        using var client = _factory.CreateClient();
        using var request = Role("SUMMIT", HttpMethod.Post, "/api/admin/run/outbox", AccessRole.Operator);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.GetProperty("worker").GetString().Should().Be("outbox");
        payload.GetProperty("processed").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Unknown_admin_worker_is_not_found()
    {
        using var client = _factory.CreateClient();
        using var request = Role("SUMMIT", HttpMethod.Post, "/api/admin/run/opportunity", AccessRole.Operator);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        payload.GetProperty("errorCode").GetString().Should().Be(Errors.WorkerNotFound.Code);
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Body)> BookMayaAsync(
        HttpClient client,
        string key,
        int credits = 0)
    {
        using var quoteRequest = Member("SUMMIT", HttpMethod.Post, $"/api/offers/{SeedIds.Offer(1).Value}/quote", SeedIds.Maya.Value);
        quoteRequest.Content = JsonContent.Create(new { stayDate = Stay });
        using var quoted = await client.SendAsync(quoteRequest);
        var quote = await quoted.Content.ReadFromJsonAsync<JsonElement>(Json);
        var quoteId = quote.GetProperty("quoteId").GetGuid();

        using var book = Member("SUMMIT", HttpMethod.Post, "/api/bookings", SeedIds.Maya.Value);
        book.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        book.Content = JsonContent.Create(new { quoteId, credits, stayDate = Stay });
        using var response = await client.SendAsync(book);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (response.StatusCode, body);
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

public sealed class BookingApiFactory : LoyaltyLabApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(entry =>
                         entry.ServiceType == typeof(IPaymentGateway) || entry.ServiceType == typeof(ISagaDelay)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IPaymentGateway, TestPaymentGateway>();
            services.AddSingleton<ISagaDelay>(ImmediateSagaDelay.Instance);
        });
    }
}

public sealed class BookingCompensationEndpointTests : IClassFixture<DecliningBookingApiFactory>
{
    private static readonly DateOnly Stay = new(2026, 3, 15);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly DecliningBookingApiFactory _factory;

    public BookingCompensationEndpointTests(DecliningBookingApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Operator_payload_includes_compensation_after_a_payment_decline()
    {
        using var client = _factory.CreateClient();
        using var quoteRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/offers/{SeedIds.Offer(1).Value}/quote");
        quoteRequest.Headers.Add(TenantResolutionMiddleware.PartnerHeader, "SUMMIT");
        quoteRequest.Headers.Add(TenantResolutionMiddleware.MemberHeader, SeedIds.Maya.Value.ToString());
        quoteRequest.Content = JsonContent.Create(new { stayDate = Stay });
        using var quoted = await client.SendAsync(quoteRequest);
        var quote = await quoted.Content.ReadFromJsonAsync<JsonElement>(Json);
        var quoteId = quote.GetProperty("quoteId").GetGuid();

        using var book = new HttpRequestMessage(HttpMethod.Post, "/api/bookings");
        book.Headers.Add(TenantResolutionMiddleware.PartnerHeader, "SUMMIT");
        book.Headers.Add(TenantResolutionMiddleware.MemberHeader, SeedIds.Maya.Value.ToString());
        book.Headers.TryAddWithoutValidation("Idempotency-Key", "idem-decline-1");
        book.Content = JsonContent.Create(new { quoteId, credits = 0, stayDate = Stay });
        using var bookedResponse = await client.SendAsync(book);
        var booked = await bookedResponse.Content.ReadFromJsonAsync<JsonElement>(Json);

        bookedResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: booked.ToString());
        booked.GetProperty("saga").GetProperty("status").GetString().Should().Be("Compensated");
        var sagaId = booked.GetProperty("saga").GetProperty("id").GetGuid();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/sagas/{sagaId}");
        request.Headers.Add(TenantResolutionMiddleware.PartnerHeader, "SUMMIT");
        request.Headers.Add(TenantResolutionMiddleware.RoleHeader, AccessRole.Operator.ToString());
        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reserved = payload.GetProperty("steps").EnumerateArray()
            .Single(step => step.GetProperty("kind").GetString() == "ReserveInventory");
        reserved.GetProperty("status").GetString().Should().Be("Compensated");
        reserved.GetProperty("attempts").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        reserved.GetProperty("compensation").GetProperty("status").GetString().Should().Be("Succeeded");
    }
}

public sealed class DecliningBookingApiFactory : LoyaltyLabApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(entry =>
                         entry.ServiceType == typeof(IPaymentGateway) || entry.ServiceType == typeof(ISagaDelay)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IPaymentGateway, DecliningPaymentGateway>();
            services.AddSingleton<ISagaDelay>(ImmediateSagaDelay.Instance);
        });
    }
}

internal sealed class DecliningPaymentGateway : IPaymentGateway
{
    public Task<StepOutcome> AuthorizeAsync(PaymentAuthorizeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        return Task.FromResult(StepOutcome.Failed(Errors.PaymentDeclined));
    }

    public Task<StepOutcome> CaptureAsync(PaymentReferenceRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        return Task.FromResult(StepOutcome.Failed(Errors.PaymentDeclined));
    }

    public Task<StepOutcome> VoidAsync(PaymentReferenceRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        return Task.FromResult(StepOutcome.Succeeded());
    }

    public Task<StepOutcome> RefundAsync(PaymentReferenceRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        return Task.FromResult(StepOutcome.Succeeded());
    }

    public Task<StepOutcome> QueryByKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        _ = idempotencyKey;
        return Task.FromResult(StepOutcome.Failed(Errors.PaymentDeclined));
    }
}

internal sealed class TestPaymentGateway : IPaymentGateway
{
    public Task<StepOutcome> AuthorizeAsync(PaymentAuthorizeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        return Task.FromResult(StepOutcome.Succeeded("pay-test"));
    }

    public Task<StepOutcome> CaptureAsync(PaymentReferenceRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        return Task.FromResult(StepOutcome.Succeeded(request.PaymentId));
    }

    public Task<StepOutcome> VoidAsync(PaymentReferenceRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        return Task.FromResult(StepOutcome.Succeeded(request.PaymentId));
    }

    public Task<StepOutcome> RefundAsync(PaymentReferenceRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        return Task.FromResult(StepOutcome.Succeeded(request.PaymentId));
    }

    public Task<StepOutcome> QueryByKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        _ = idempotencyKey;
        return Task.FromResult(StepOutcome.Succeeded("pay-test"));
    }
}
