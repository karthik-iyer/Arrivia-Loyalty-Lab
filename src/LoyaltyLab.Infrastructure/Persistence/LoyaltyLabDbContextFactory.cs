using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LoyaltyLab.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` create the context without running the API host.
/// </summary>
public sealed class LoyaltyLabDbContextFactory : IDesignTimeDbContextFactory<LoyaltyLabDbContext>
{
    public LoyaltyLabDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LoyaltyLabDbContext>()
            .UseSqlite("Data Source=loyaltylab.db")
            .Options;

        var tenant = new MutableTenantContextAccessor();
        tenant.Set(TenantContext.Anonymous(PartnerId.New()));
        return new LoyaltyLabDbContext(options, tenant);
    }
}
