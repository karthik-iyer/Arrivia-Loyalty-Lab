using System.Text.Json;
using System.Text.Json.Serialization;
using LoyaltyLab.Api.FaultInjection;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Infrastructure.Suppliers;
using Microsoft.Extensions.Options;

namespace LoyaltyLab.Api.Middleware;

/// <summary>
/// Applies <c>X-Fault-Profile</c> (or the global config profile) when injection is enabled (FR-B-09).
/// </summary>
public sealed class FaultInjectionMiddleware(RequestDelegate next, IOptions<FaultProfile> global)
{
    public const string HeaderName = "X-Fault-Profile";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task InvokeAsync(
        HttpContext context,
        RequestFaultProfileAccessor accessor,
        SupplierFaultHooks hooks)
    {
        var profile = Parse(context.Request.Headers[HeaderName].FirstOrDefault()) ?? global.Value;
        accessor.Replace(profile);
        hooks.Apply(profile);
        await next(context);
    }

    private static FaultProfile? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FaultProfile>(raw, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
