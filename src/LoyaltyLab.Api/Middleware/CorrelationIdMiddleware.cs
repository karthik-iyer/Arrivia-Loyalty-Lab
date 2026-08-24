using System.Diagnostics;

namespace LoyaltyLab.Api.Middleware;

/// <summary>
/// Accepts or assigns a correlation id and echoes it on the response (FR-X-08).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.CreateVersion7().ToString();

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        await next(context);
    }
}
