using LoyaltyLab.Api.Endpoints;
using LoyaltyLab.Api.FaultInjection;
using LoyaltyLab.Api.Http;
using LoyaltyLab.Api.Mcp;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Api.Workers;
using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Booking;
using LoyaltyLab.Application.Concierge;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Application.Opportunity;
using LoyaltyLab.Application.Pricing;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Tenancy;
using LoyaltyLab.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

FaultInjectionStartup.EnsureAllowed(builder.Environment, builder.Configuration);

builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddLoyaltyLabInfrastructure();
builder.Services.AddHostedService<OutboxDispatcherWorker>();
builder.Services.AddHostedService<SagaRecoveryWorker>();
builder.Services.AddSingleton<IClock>(sp => CreateClock(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IOfferNarrator, NullOfferNarrator>();
builder.Services.AddScoped<SearchOffers>();
builder.Services.AddScoped<QuoteOffer>();
builder.Services.AddScoped<ExplainQuote>();
builder.Services.AddScoped<Recommend>();
builder.Services.AddScoped<DetectTravelWindows>();
builder.Services.AddScoped<EvaluateOpportunities>();
builder.Services.AddScoped<TenantBinder>();
builder.Services.AddScoped<IMcpUseCases, McpUseCases>();
builder.Services.AddScoped<ClaimIdempotency>();
builder.Services.AddScoped<EarnCredits>();
builder.Services.AddScoped<BurnCredits>();
builder.Services.AddScoped<ExpireCredits>();
builder.Services.AddScoped<ReverseLedger>();
builder.Services.AddScoped<AdjustCredits>();
builder.Services.AddScoped<GetBalance>();
builder.Services.AddScoped<GetStatement>();
builder.Services.AddScoped<GetLiabilityReport>();
builder.Services.AddScoped<ReconcileLedger>();
builder.Services.AddScoped<ExpireDueCredits>();
builder.Services.AddScoped<ValidateQuoteStep>();
builder.Services.AddScoped<ReserveInventoryStep>();
builder.Services.AddScoped<AuthorizePaymentStep>();
builder.Services.AddScoped<BurnCreditsStep>();
builder.Services.AddScoped<CapturePaymentStep>();
builder.Services.AddScoped<ConfirmBookingStep>();
builder.Services.AddScoped<IReadOnlyList<ISagaStep>>(sp =>
[
    sp.GetRequiredService<ValidateQuoteStep>(),
    sp.GetRequiredService<ReserveInventoryStep>(),
    sp.GetRequiredService<AuthorizePaymentStep>(),
    sp.GetRequiredService<BurnCreditsStep>(),
    sp.GetRequiredService<CapturePaymentStep>(),
    sp.GetRequiredService<ConfirmBookingStep>(),
]);
builder.Services.AddSingleton<ISagaDelay>(ExponentialSagaDelay.Instance);
builder.Services.AddScoped<AdvanceSaga>();
builder.Services.AddScoped<RecoverStalledSagas>();
builder.Services.AddScoped<StartBookingSaga>();
builder.Services.AddScoped<GetBooking>();
builder.Services.AddScoped<CancelBooking>();
builder.Services.AddScoped<GetSagaInstance>();
builder.Services.AddScoped<ListSagas>();
builder.Services.AddScoped<RunAdminWorker>();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<ConciergeTools>();

builder.Host.ConfigureServices((context, services) =>
{
    if (FaultInjectionStartup.IsEnabled(context.Configuration))
    {
        services.AddFaultInjection(context.Configuration);
    }
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

FaultInjectionStartup.EnsureAllowed(app.Environment, app.Configuration);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<TenantResolutionMiddleware>();
if (FaultInjectionStartup.IsEnabled(app.Configuration))
{
    app.UseMiddleware<FaultInjectionMiddleware>();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPricingEndpoints();
app.MapWalletEndpoints();
app.MapBookingEndpoints();
app.MapConciergeEndpoints();
app.MapMcp("/mcp");

app.MapGet("/api/partners/current/theme", async (
    ITenantContextAccessor tenant,
    LoyaltyLabDbContext db,
    CancellationToken cancellationToken) =>
{
    var partner = await db.Partners
        .AsNoTracking()
        .SingleAsync(p => p.Id == tenant.Current.PartnerId, cancellationToken);

    return Results.Ok(new
    {
        code = partner.Code,
        displayName = partner.DisplayName,
        primaryColor = partner.Theme.PrimaryColor,
        surfaceColor = partner.Theme.SurfaceColor,
        accentColor = partner.Theme.AccentColor,
        logoUrl = partner.Theme.LogoUrl,
    });
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LoyaltyLabDbContext>();
    var tenant = scope.ServiceProvider.GetRequiredService<MutableTenantContextAccessor>();
    tenant.Set(TenantContext.Anonymous(SeedIds.Summit));
    await db.Database.MigrateAsync();
    await DemoSeed.EnsureAsync(db);
}

await app.RunAsync();

static IClock CreateClock(IConfiguration configuration)
{
    var section = configuration.GetSection("DemoClock");
    if (!section.GetValue("Enabled", false))
    {
        return new SystemClock();
    }

    var instant = section.GetValue<DateTimeOffset?>("UtcNow")
        ?? new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
    return new FixedDemoClock(instant);
}

public partial class Program;
