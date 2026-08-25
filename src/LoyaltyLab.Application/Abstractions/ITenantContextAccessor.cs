using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Abstractions;

/// <summary>
/// Request-scoped partner and member. Persistence uses this for tenant query filters (FR-X-02).
/// </summary>
public interface ITenantContextAccessor
{
    TenantContext Current { get; }

    bool HasCurrent { get; }

    void Assign(TenantContext context);
}
