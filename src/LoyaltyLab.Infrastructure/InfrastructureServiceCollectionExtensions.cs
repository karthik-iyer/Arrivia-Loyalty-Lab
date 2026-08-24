using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
