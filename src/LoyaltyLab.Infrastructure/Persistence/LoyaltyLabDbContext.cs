using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Idempotency;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Opportunity;
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

    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();

    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<SagaInstance> SagaInstances => Set<SagaInstance>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<PoisonMessage> PoisonMessages => Set<PoisonMessage>();

    public DbSet<BusyPeriod> BusyPeriods => Set<BusyPeriod>();

    public DbSet<Nudge> Nudges => Set<Nudge>();

    public DbSet<PriceWatch> PriceWatches => Set<PriceWatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoyaltyLabDbContext).Assembly);

        // Tenant isolation is structural: a forgotten Where cannot leak members (FR-X-02).
        modelBuilder.Entity<Member>().HasQueryFilter(m => m.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<PartnerSupplier>().HasQueryFilter(p => p.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<Quote>().HasQueryFilter(q => q.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<PricingRule>().HasQueryFilter(r => r.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<LedgerAccount>().HasQueryFilter(a => a.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<LedgerTransaction>().HasQueryFilter(t => t.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<IdempotencyRecord>().HasQueryFilter(r => r.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<SagaInstance>().HasQueryFilter(s => s.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<Booking>().HasQueryFilter(b => b.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(m => m.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<PoisonMessage>().HasQueryFilter(m => m.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<BusyPeriod>().HasQueryFilter(p => p.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<Nudge>().HasQueryFilter(n => n.PartnerId == _tenant.Current.PartnerId);
        modelBuilder.Entity<PriceWatch>().HasQueryFilter(w => w.PartnerId == _tenant.Current.PartnerId);
    }
}
