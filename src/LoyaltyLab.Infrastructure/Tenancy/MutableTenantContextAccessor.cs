using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Infrastructure.Tenancy;

/// <summary>
/// Holds the resolved tenant for the current scope. Middleware sets it; the DbContext reads it.
/// Registered scoped so instance state is request-wide without ambient statics.
/// </summary>
public sealed class MutableTenantContextAccessor : ITenantContextAccessor
{
    private TenantContext? _current;

    public TenantContext Current =>
        _current ?? throw new InvalidOperationException("No tenant context is set for this request.");

    public bool HasCurrent => _current is not null;

    public void Set(TenantContext context) => _current = context;

    public void Assign(TenantContext context) => Set(context);

    public void Clear() => _current = null;
}
