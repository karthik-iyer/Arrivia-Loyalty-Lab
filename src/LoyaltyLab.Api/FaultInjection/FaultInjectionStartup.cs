using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;

namespace LoyaltyLab.Api.FaultInjection;

/// <summary>
/// NFR-14: injection is off by default and cannot be turned on in Production.
/// </summary>
public static class FaultInjectionStartup
{
    public const string ConfigKey = "Features:FaultInjection";

    public const string FailFastConfigKey = "Features:SimulatedCrashFailFast";

    public const string ProfileSection = "FaultProfile";

    public static bool CrashFailFast(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetValue(FailFastConfigKey, true);
    }

    public static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetValue(ConfigKey, false);
    }

    public static void EnsureAllowed(IHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        if (IsEnabled(configuration) && environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Fault injection cannot be enabled in a production environment (NFR-14).");
        }
    }

    public static void AddFaultInjection(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<FaultProfile>(configuration.GetSection(ProfileSection));
        services.AddScoped<RequestFaultProfileAccessor>();
        services.AddScoped<IFaultInjector>(sp => sp.GetRequiredService<RequestFaultProfileAccessor>());
    }
}
