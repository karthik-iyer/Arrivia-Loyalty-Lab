using System.Net;
using System.Text;
using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace LoyaltyLab.Api.Tests.Payments;

public sealed class HttpPaymentGatewayTests
{
    [Fact]
    public async Task Forced_timeout_produces_unknown()
    {
        var gateway = CreateGateway(new SlowHandler(TimeSpan.FromSeconds(5)), retryCount: 1, attemptTimeoutMs: 50);

        var outcome = await gateway.AuthorizeAsync(Authorize(), CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Unknown);
        outcome.Error.Should().BeNull();
        outcome.ExternalReference.Should().BeNull();
    }

    [Fact]
    public async Task Authorized_response_is_success_with_the_payment_id()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var gateway = CreateGateway(new JsonHandler(HttpStatusCode.Created, $$"""{"id":"{{id}}","status":"Authorized"}"""));

        var outcome = await gateway.AuthorizeAsync(Authorize(), CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Succeeded);
        outcome.ExternalReference.Should().Be(id.ToString());
    }

    [Fact]
    public async Task Decline_is_failed_not_unknown()
    {
        var gateway = CreateGateway(new JsonHandler(HttpStatusCode.PaymentRequired, """{"id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","status":"Declined"}"""));

        var outcome = await gateway.AuthorizeAsync(Authorize(), CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Failed);
        outcome.Error.Should().Be(Errors.PaymentDeclined);
    }

    [Fact]
    public async Task Query_by_key_returns_the_stored_reference()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var gateway = CreateGateway(new JsonHandler(HttpStatusCode.OK, $$"""{"id":"{{id}}","status":"Authorized"}"""));

        var outcome = await gateway.QueryByKeyAsync("saga-1:AuthorizePayment", CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Succeeded);
        outcome.ExternalReference.Should().Be(id.ToString());
    }

    [Fact]
    public async Task PaymentDecline_sends_the_force_decline_header()
    {
        var capture = new CaptureHandler(HttpStatusCode.PaymentRequired, """{"id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","status":"Declined"}""");
        var gateway = CreateGateway(capture, faults: new FaultProfile(PaymentDecline: true));

        var outcome = await gateway.AuthorizeAsync(Authorize(), CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Failed);
        outcome.Error.Should().Be(Errors.PaymentDeclined);
        capture.Headers.Should().Contain("X-Sim-Force-Decline");
        capture.Headers.Should().NotContain("X-Sim-Force-Timeout");
    }

    [Fact]
    public async Task PaymentTimeout_sends_the_force_timeout_header_and_maps_408_to_unknown()
    {
        var capture = new CaptureHandler(HttpStatusCode.RequestTimeout, body: null);
        var gateway = CreateGateway(capture, faults: new FaultProfile(PaymentTimeout: true));

        var outcome = await gateway.AuthorizeAsync(Authorize(), CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Unknown);
        outcome.ExternalReference.Should().BeNull();
        capture.Headers.Should().Contain("X-Sim-Force-Timeout");
    }

    private static IPaymentGateway CreateGateway(
        HttpMessageHandler handler,
        int retryCount = 0,
        int attemptTimeoutMs = 2_000,
        FaultProfile? faults = null)
    {
        var services = new ServiceCollection();
        if (faults is not null)
        {
            services.AddSingleton<IFaultInjector>(new FixedFaultInjector(faults));
        }

        var builder = services.AddHttpClient<IPaymentGateway, HttpPaymentGateway>(client =>
        {
            client.BaseAddress = new Uri("http://payment.test/");
        });
        builder.ConfigurePrimaryHttpMessageHandler(() => handler);
        builder.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(attemptTimeoutMs);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMilliseconds(Math.Max(attemptTimeoutMs * 4, 200));
            options.Retry.MaxRetryAttempts = Math.Max(retryCount, 1);
            options.Retry.Delay = TimeSpan.FromMilliseconds(10);
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMilliseconds(Math.Max(attemptTimeoutMs * 2, 500));
        });

        return services.BuildServiceProvider().GetRequiredService<IPaymentGateway>();
    }

    private static PaymentAuthorizeRequest Authorize() =>
        new(Money.Of(120.75m, Currency.Usd), "saga-1:AuthorizePayment", "Coral Bay");

    private sealed class SlowHandler(TimeSpan delay) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class JsonHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class CaptureHandler(HttpStatusCode status, string? body) : HttpMessageHandler
    {
        public List<string> Headers { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Headers.AddRange(request.Headers.Select(header => header.Key));
            var response = new HttpResponseMessage(status);
            if (body is not null)
            {
                response.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }

    private sealed class FixedFaultInjector(FaultProfile profile) : IFaultInjector
    {
        public FaultProfile Current { get; } = profile;
    }
}
