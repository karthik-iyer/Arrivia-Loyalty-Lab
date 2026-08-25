using LoyaltyLab.Api.FaultInjection;
using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Booking;
using LoyaltyLab.Infrastructure.Payments;
using LoyaltyLab.Resilience.Tests.PaymentSim;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoyaltyLab.Resilience.Tests.Booking;

/// <summary>
/// API host pointed at the real PaymentSim TestServer (ADR-0006). SQLite is shared
/// across a kill/restart by passing the same database path to a second factory.
/// </summary>
public sealed class ChaosApiFactory : WebApplicationFactory<Program>
{
    private readonly PaymentSimFactory _payments;
    private readonly HttpClient _paymentClient;
    private readonly string _clock;
    private readonly bool _deleteDatabase;

    public ChaosApiFactory(
        PaymentSimFactory payments,
        string dbPath,
        string clock = "2026-03-15T12:00:00+00:00",
        bool deleteDatabase = true)
    {
        _payments = payments;
        DbPath = dbPath;
        _clock = clock;
        _deleteDatabase = deleteDatabase;
        _paymentClient = payments.CreateClient();
    }

    public string DbPath { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(FaultInjectionStartup.ConfigKey, "true");
        builder.UseSetting(FaultInjectionStartup.FailFastConfigKey, "false");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LoyaltyLab"] = $"Data Source={DbPath};Pooling=False",
                ["DemoClock:Enabled"] = "true",
                ["DemoClock:UtcNow"] = _clock,
                ["Outbox:Dispatcher:Enabled"] = "false",
                ["SagaRecovery:Enabled"] = "false",
                [FaultInjectionStartup.ConfigKey] = "true",
                [FaultInjectionStartup.FailFastConfigKey] = "false",
            });
        });
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(entry =>
                         entry.ServiceType == typeof(IPaymentGateway) || entry.ServiceType == typeof(ISagaDelay)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IPaymentGateway>(sp =>
                new HttpPaymentGateway(_paymentClient, sp.GetServices<IFaultInjector>()));
            services.AddSingleton<ISagaDelay>(ImmediateSagaDelay.Instance);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || !_deleteDatabase)
        {
            return;
        }

        try
        {
            File.Delete(DbPath);
        }
        catch (IOException)
        {
        }
    }
}
