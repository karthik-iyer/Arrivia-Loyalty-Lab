using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class LoyaltyLabDbContext : DbContext
{
    private readonly ITenantContextAccessor _tenant;

    public LoyaltyLabDbContext(DbContextOptions<LoyaltyLabDbContext> options, ITenantContextAccessor tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Partner> Partners => Set<Partner>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<TravelOffer> Offers => Set<TravelOffer>();

    public DbSet<PartnerSupplier> PartnerSuppliers => Set<PartnerSupplier>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<PricingRule> PricingRules => Set<PricingRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoyaltyLabDbContext).Assembly);

        // Tenant isolation is structural: a forgotten Where cannot leak members (FR-X-02).
        modelBuilder.Entity<Member>().HasQueryFilter(m => m.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<PartnerSupplier>().HasQueryFilter(p => p.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<Quote>().HasQueryFilter(q => q.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<PricingRule>().HasQueryFilter(r => r.PartnerId == _tenant.Current.PartnerId);
    }
}
