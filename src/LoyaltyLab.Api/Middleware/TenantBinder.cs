using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Persistence;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Api.Middleware;

/// <summary>
/// Shared tenant assignment for REST headers and MCP tool arguments (FR-X-01, FR-C-08).
/// </summary>
public sealed class TenantBinder(LoyaltyLabDbContext db, MutableTenantContextAccessor tenant)
{
    public async Task<Error?> BindAsync(
        string? partnerCode,
        string? memberId,
        string? role,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(partnerCode))
        {
            return Errors.PartnerNotResolved;
        }

        var normalized = partnerCode.Trim().ToUpperInvariant();
        var partner = await db.Partners
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == normalized, cancellationToken);

        if (partner is null)
        {
            return Errors.PartnerNotResolved;
        }

        tenant.Set(TenantContext.Anonymous(partner.Id));

        var parsedRole = ParseRole(role);
        Member? member = null;
        if (Guid.TryParse(memberId, out var memberGuid))
        {
            member = await db.Members
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == new MemberId(memberGuid), cancellationToken);
        }

        if (parsedRole is AccessRole.AccountManager or AccessRole.FinanceAnalyst or AccessRole.Operator)
        {
            tenant.Set(
                member is null
                    ? TenantContext.ForRole(partner.Id, parsedRole.Value)
                    : new TenantContext(partner.Id, member.Id, member.Tier, parsedRole.Value));
        }
        else if (member is not null)
        {
            tenant.Set(TenantContext.ForMember(member));
        }

        return null;
    }

    private static AccessRole? ParseRole(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        return Enum.TryParse<AccessRole>(header.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }
}
