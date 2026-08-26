using LoyaltyLab.Api.FaultInjection;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Infrastructure.Suppliers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LoyaltyLab.Api.Tests.Hosting;

public sealed class FaultInjectionTests : IClassFixture<LoyaltyLabApiFactory>
{
    private readonly LoyaltyLabApiFactory _factory;

    public FaultInjectionTests(LoyaltyLabApiFactory factory) => _factory = factory;

    [Fact]
    public void Production_refuses_to_start_when_fault_injection_is_on()
    {
        using var factory = new ProductionFaultInjectionApiFactory();

        var act = () => factory.CreateClient();

        Flatten(act.Should().Throw<Exception>().Which)
            .Should()
            .Contain(ex => ex is InvalidOperationException && ex.Message.Contains("production", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureAllowed_throws_only_in_production_when_enabled()
    {
        var enabled = Config(("Features:FaultInjection", "true"));
        var disabled = Config(("Features:FaultInjection", "false"));

        var productionEnabled = () => FaultInjectionStartup.EnsureAllowed(new StubEnvironment("Production"), enabled);
        productionEnabled.Should().Throw<InvalidOperationException>().WithMessage("*production*");

        var act = () => FaultInjectionStartup.EnsureAllowed(new StubEnvironment("Development"), enabled);
        act.Should().NotThrow();

        var productionOff = () => FaultInjectionStartup.EnsureAllowed(new StubEnvironment("Production"), disabled);
        productionOff.Should().NotThrow();
    }

    [Fact]
    public void Injector_is_not_registered_when_the_flag_is_off()
    {
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetService<IFaultInjector>().Should().BeNull();
    }

    [Fact]
    public async Task Development_starts_when_fault_injection_is_on()
    {
        using var factory = new DevelopmentFaultInjectionApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, because: body);
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetService<IFaultInjector>().Should().BeOfType<RequestFaultProfileAccessor>();
    }

    [Fact]
    public async Task Header_applies_supplier_timeout_and_decline()
    {
        var hooks = new SupplierFaultHooks();
        var accessor = new RequestFaultProfileAccessor(Options.Create(FaultProfile.None));
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };
        var middleware = new FaultInjectionMiddleware(next, Options.Create(FaultProfile.None));
        var context = new DefaultHttpContext();
        context.Request.Headers[FaultInjectionMiddleware.HeaderName] =
            """{"supplierTimeout":true,"supplierDecline":true,"addedLatencyMs":25}""";

        await middleware.InvokeAsync(context, accessor, hooks);

        invoked.Should().BeTrue();
        accessor.Current.SupplierTimeout.Should().BeTrue();
        accessor.Current.SupplierDecline.Should().BeTrue();
        accessor.Current.AddedLatencyMs.Should().Be(25);
        hooks.TimeoutOnReserve.Should().BeTrue();
        hooks.DeclineOnReserve.Should().BeTrue();
        hooks.AddedLatencyMs.Should().Be(25);
    }

    [Fact]
    public async Task Header_overrides_the_global_profile()
    {
        var global = new FaultProfile(SupplierTimeout: true, CrashAfterStep: SagaStepKind.ValidateQuote);
        var hooks = new SupplierFaultHooks();
        var accessor = new RequestFaultProfileAccessor(Options.Create(global));
        var middleware = new FaultInjectionMiddleware(_ => Task.CompletedTask, Options.Create(global));
        var context = new DefaultHttpContext();
        context.Request.Headers[FaultInjectionMiddleware.HeaderName] =
            """{"crashAfterStep":"ReserveInventory"}""";

        await middleware.InvokeAsync(context, accessor, hooks);

        accessor.Current.SupplierTimeout.Should().BeFalse();
        accessor.Current.CrashAfterStep.Should().Be(SagaStepKind.ReserveInventory);
        hooks.TimeoutOnReserve.Should().BeFalse();
    }

    [Fact]
    public async Task Malformed_header_keeps_the_global_profile()
    {
        var global = new FaultProfile(PaymentDecline: true);
        var hooks = new SupplierFaultHooks();
        var accessor = new RequestFaultProfileAccessor(Options.Create(global));
        var middleware = new FaultInjectionMiddleware(_ => Task.CompletedTask, Options.Create(global));
        var context = new DefaultHttpContext();
        context.Request.Headers[FaultInjectionMiddleware.HeaderName] = "{not-json";

        await middleware.InvokeAsync(context, accessor, hooks);

        accessor.Current.Should().Be(global);
        hooks.TimeoutOnReserve.Should().BeFalse();
    }

    private static IConfiguration Config(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        yield return exception;
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                foreach (var nested in Flatten(inner))
                {
                    yield return nested;
                }
            }

            yield break;
        }

        if (exception.InnerException is { } next)
        {
            foreach (var nested in Flatten(next))
            {
                yield return nested;
            }
        }
    }

    private sealed class StubEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "LoyaltyLab.Api";

        public string ContentRootPath { get; set; } = ".";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public sealed class DevelopmentFaultInjectionApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"loyaltylab-fault-dev-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(FaultInjectionStartup.ConfigKey, "true");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LoyaltyLab"] = $"Data Source={_dbPath}",
                ["DemoClock:Enabled"] = "true",
                ["DemoClock:UtcNow"] = "2026-03-15T12:00:00+00:00",
                ["Outbox:Dispatcher:Enabled"] = "false",
                ["SagaRecovery:Enabled"] = "false",
                ["OpportunityScan:Enabled"] = "false",
                ["Features:FaultInjection"] = "true",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
            }
        }
    }
}

public sealed class ProductionFaultInjectionApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"loyaltylab-fault-prod-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(FaultInjectionStartup.ConfigKey, "true");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LoyaltyLab"] = $"Data Source={_dbPath}",
                ["DemoClock:Enabled"] = "false",
                ["Outbox:Dispatcher:Enabled"] = "false",
                ["SagaRecovery:Enabled"] = "false",
                ["OpportunityScan:Enabled"] = "false",
                ["Features:FaultInjection"] = "true",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
            }
        }
    }
}
