using LoyaltyLab.Api.Endpoints;
using LoyaltyLab.Api.Middleware;
using LoyaltyLab.Api.Workers;
using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Idempotency;
using LoyaltyLab.Application.Loyalty;
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

builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddLoyaltyLabInfrastructure();
builder.Services.AddHostedService<OutboxDispatcherWorker>();
builder.Services.AddSingleton<IClock>(sp => CreateClock(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddScoped<SearchOffers>();
builder.Services.AddScoped<QuoteOffer>();
builder.Services.AddScoped<ExplainQuote>();
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
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPricingEndpoints();
app.MapWalletEndpoints();

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
