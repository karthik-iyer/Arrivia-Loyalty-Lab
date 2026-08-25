using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Infrastructure.Payments;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Persistence.Outbox;
using LoyaltyLab.Infrastructure.Suppliers;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace LoyaltyLab.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddLoyaltyLabInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<MutableTenantContextAccessor>();
        services.AddScoped<ITenantContextAccessor>(sp => sp.GetRequiredService<MutableTenantContextAccessor>());
        services.AddDbContext<LoyaltyLabDbContext>((sp, options) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("LoyaltyLab") ?? "Data Source=loyaltylab.db";
            options.UseSqlite(connectionString);
        });
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IPartnerRepository, PartnerRepository>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<IPartnerSupplierRepository, PartnerSupplierRepository>();
        services.AddScoped<IPricingRuleRepository, PricingRuleRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<ISagaRepository, SagaRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddOptions<SagaRecoveryOptions>().BindConfiguration(SagaRecoveryOptions.SectionName);
        services.AddScoped<IOutbox, EfOutbox>();
        services.AddScoped<OutboxDispatcher>();
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.SectionName);
        foreach (var type in OutboxMessageTypes.All)
        {
            services.AddSingleton<IOutboxHandler>(new NoOpOutboxHandler(type));
        }

        services.AddScoped<IBookingTenderQuery, BookingTenderQuery>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<SupplierReservationStore>();
        services.AddScoped<SupplierFaultHooks>();
        services.AddScoped<ISupplierClient>(sp => new SimulatedSupplierClient(
            sp.GetRequiredService<IOfferRepository>(),
            sp.GetRequiredService<SupplierReservationStore>(),
            sp.GetRequiredService<SupplierFaultHooks>(),
            sp.GetRequiredService<IClock>()));
        services.AddPaymentGateway();
        return services;
    }

    public static IHttpClientBuilder AddPaymentGateway(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<PaymentGatewayOptions>().BindConfiguration(PaymentGatewayOptions.SectionName);

        var builder = services.AddHttpClient<IPaymentGateway, HttpPaymentGateway>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PaymentGatewayOptions>>().Value;
            client.BaseAddress = new Uri(TrailingSlash(options.BaseUrl));
        });
        builder.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(100);
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
        });
        return builder;
    }

    private static string TrailingSlash(string url) =>
        url.Length > 0 && url[^1] == '/' ? url : url + "/";
}
