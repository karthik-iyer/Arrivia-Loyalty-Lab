using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Api.Middleware;

/// <summary>
/// Resolves X-Partner-Code, optional X-Member-Id, and optional X-Access-Role
/// before any business logic (FR-X-01, FR-X-03). Health, MCP, and open API paths skip
/// header resolution so probes and tool arguments supply tenant themselves.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string PartnerHeader = "X-Partner-Code";

    public const string MemberHeader = "X-Member-Id";

    public const string RoleHeader = "X-Access-Role";

    public async Task InvokeAsync(HttpContext context, TenantBinder binder)
    {
        if (IsAnonymousPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var error = await binder.BindAsync(
            context.Request.Headers[PartnerHeader].FirstOrDefault(),
            context.Request.Headers[MemberHeader].FirstOrDefault(),
            context.Request.Headers[RoleHeader].FirstOrDefault(),
            context.RequestAborted);

        if (error is not null)
        {
            await WritePartnerNotResolvedAsync(context);
            return;
        }

        await next(context);
    }

    private static bool IsAnonymousPath(PathString path) =>
        path.StartsWithSegments("/health")
        || path.StartsWithSegments("/alive")
        || path.StartsWithSegments("/favicon.ico")
        || path.StartsWithSegments("/mcp")
        || path.StartsWithSegments("/openapi")
        || path.StartsWithSegments("/scalar");

    private static Task WritePartnerNotResolvedAsync(HttpContext context)
    {
        var error = Errors.PartnerNotResolved;
        return Results.Problem(
            title: error.Message,
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = error.Code,
                ["correlationId"] = context.TraceIdentifier,
            }).ExecuteAsync(context);
    }
}
