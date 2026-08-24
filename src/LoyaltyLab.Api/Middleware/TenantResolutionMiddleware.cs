using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Api.Middleware;

/// <summary>
/// Resolves X-Partner-Code (and optional X-Member-Id) before any business logic (FR-X-01, FR-X-03).
/// Health and open API paths skip resolution so probes do not need a tenant.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string PartnerHeader = "X-Partner-Code";

    public const string MemberHeader = "X-Member-Id";

    public async Task InvokeAsync(
        HttpContext context,
        LoyaltyLabDbContext db,
        MutableTenantContextAccessor tenant)
    {
        if (IsAnonymousPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var code = context.Request.Headers[PartnerHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(code))
        {
            await WritePartnerNotResolvedAsync(context);
            return;
        }

        var normalized = code.Trim().ToUpperInvariant();
        var partner = await db.Partners
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == normalized, context.RequestAborted);

        if (partner is null)
        {
            await WritePartnerNotResolvedAsync(context);
            return;
        }

        tenant.Set(TenantContext.Anonymous(partner.Id));

        var memberHeader = context.Request.Headers[MemberHeader].FirstOrDefault();
        if (Guid.TryParse(memberHeader, out var memberGuid))
        {
            var member = await db.Members
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == new MemberId(memberGuid), context.RequestAborted);

            if (member is not null)
            {
                tenant.Set(TenantContext.ForMember(member));
            }
        }

        await next(context);
    }

    private static bool IsAnonymousPath(PathString path) =>
        path.StartsWithSegments("/health")
        || path.StartsWithSegments("/alive")
        || path.StartsWithSegments("/favicon.ico");

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
