using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Idempotency;
using LoyaltyLab.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyLab.Infrastructure.Persistence;

internal sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable("Partners");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(p => p.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();
        builder.Property(p => p.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Currency).HasConversion(c => c.Code, v => Currency.Of(v));
        builder.Property(p => p.Theme).HasConversion(PersistenceJson.JsonConverter<PartnerTheme>());
        builder.Property(p => p.CreditPolicy).HasConversion(PersistenceJson.JsonConverter<CreditPolicy>());
        builder.Property(p => p.QuotePolicy).HasConversion(PersistenceJson.JsonConverter<QuotePolicy>());
        builder.Property(p => p.SagaPolicy).HasConversion(PersistenceJson.JsonConverter<SagaPolicy>());
        builder.Property(p => p.OpportunityPolicy).HasConversion(PersistenceJson.JsonConverter<OpportunityPolicy>());
    }
}

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasConversion(id => id.Value, v => new MemberId(v));
        builder.Property(m => m.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(m => m.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Tier).HasConversion<string>();
        builder.HasIndex(m => new { m.PartnerId, m.Id });
    }
}

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, v => new SupplierId(v));
        builder.Property(s => s.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
    }
}

internal sealed class TravelOfferConfiguration : IEntityTypeConfiguration<TravelOffer>
{
    public void Configure(EntityTypeBuilder<TravelOffer> builder)
    {
        builder.ToTable("TravelOffers");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasConversion(id => id.Value, v => new OfferId(v));
        builder.Property(o => o.SupplierId).HasConversion(id => id.Value, v => new SupplierId(v));
        builder.Property(o => o.PropertyName).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Destination).HasConversion(new DestinationConverter());
        builder.Property(o => o.NetRate).HasConversion(new MoneyConverter());
        builder.Property(o => o.TaxesAndFees).HasConversion(new MoneyConverter());
        builder.Property(o => o.Tags).HasConversion(
            PersistenceJson.JsonConverter<HashSet<OfferTag>>(),
            PersistenceJson.CollectionComparer<HashSet<OfferTag>>());
        builder.HasIndex(o => new { o.SupplierId, o.AvailableFrom, o.AvailableTo });
    }
}

internal sealed class QuoteConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Pricing.Quote>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Pricing.Quote> builder)
    {
        builder.ToTable("Quotes");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasConversion(id => id.Value, v => new QuoteId(v));
        builder.Property(q => q.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(q => q.MemberId).HasConversion(id => id.Value, v => new MemberId(v));
        builder.Property(q => q.OfferId).HasConversion(id => id.Value, v => new OfferId(v));
        builder.Property(q => q.NetRateSnapshot).HasConversion(new MoneyConverter());
        builder.Property(q => q.NetCostSnapshot).HasConversion(new MoneyConverter());
        builder.Property(q => q.MemberPrice).HasConversion(new MoneyConverter());
        builder.Property(q => q.MaxCreditTender).HasConversion(new MoneyConverter());
        builder.Property(q => q.Trace).HasConversion(
            PersistenceJson.JsonConverter<List<LoyaltyLab.Domain.Pricing.PriceTraceEntry>>(),
            PersistenceJson.CollectionComparer<List<LoyaltyLab.Domain.Pricing.PriceTraceEntry>>());
        builder.HasIndex(q => new { q.PartnerId, q.MemberId, q.ExpiresAt });
    }
}

internal sealed class BookingConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Booking.Booking>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Booking.Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasConversion(id => id.Value, v => new BookingId(v));
        builder.Property(b => b.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(b => b.MemberId).HasConversion(id => id.Value, v => new MemberId(v));
        builder.Property(b => b.QuoteId).HasConversion(id => id.Value, v => new QuoteId(v));
        builder.Property(b => b.Tender)
            .HasConversion(PersistenceJson.JsonConverter<LoyaltyLab.Domain.Booking.TenderSplit>());
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(b => b.Drift)
            .HasConversion(PersistenceJson.NullableJsonConverter<LoyaltyLab.Domain.Pricing.RateDriftOutcome>());
        builder.Property(b => b.SupplierReference).HasMaxLength(128);
    }
}

internal sealed class PricingRuleConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Pricing.PricingRule>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Pricing.PricingRule> builder)
    {
        builder.ToTable("PricingRules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, v => new PricingRuleId(v));
        builder.Property(r => r.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(r => r.Scope).HasConversion(PersistenceJson.JsonConverter<LoyaltyLab.Domain.Pricing.RuleScope>());
        builder.Ignore(r => r.Specificity);
        builder.HasIndex(r => new { r.PartnerId, r.Kind, r.EffectiveFrom, r.EffectiveTo });
        builder.HasDiscriminator(r => r.Kind)
            .HasValue<LoyaltyLab.Domain.Pricing.EligibilityExclusionRule>(
                LoyaltyLab.Domain.Pricing.PricingRuleKind.EligibilityExclusion)
            .HasValue<LoyaltyLab.Domain.Pricing.BaseMarkupRule>(
                LoyaltyLab.Domain.Pricing.PricingRuleKind.BaseMarkup)
            .HasValue<LoyaltyLab.Domain.Pricing.TierAdjustmentRule>(
                LoyaltyLab.Domain.Pricing.PricingRuleKind.TierAdjustment)
            .HasValue<LoyaltyLab.Domain.Pricing.CampaignDiscountRule>(
                LoyaltyLab.Domain.Pricing.PricingRuleKind.CampaignDiscount)
            .HasValue<LoyaltyLab.Domain.Pricing.MarginFloorRule>(
                LoyaltyLab.Domain.Pricing.PricingRuleKind.MarginFloor)
            .HasValue<LoyaltyLab.Domain.Pricing.BurnCapRule>(
                LoyaltyLab.Domain.Pricing.PricingRuleKind.BurnCap);
    }
}

internal sealed class BaseMarkupRuleConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Pricing.BaseMarkupRule>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Pricing.BaseMarkupRule> builder) =>
        builder.Property(r => r.Markup).HasConversion(new PercentConverter());
}

internal sealed class TierAdjustmentRuleConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Pricing.TierAdjustmentRule>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Pricing.TierAdjustmentRule> builder) =>
        builder.Property(r => r.Adjustment).HasConversion(new PercentConverter()).HasColumnName("Adjustment");
}

internal sealed class CampaignDiscountRuleConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Pricing.CampaignDiscountRule>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Pricing.CampaignDiscountRule> builder)
    {
        builder.Property(r => r.CampaignCode).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Adjustment).HasConversion(new PercentConverter()).HasColumnName("Adjustment");
    }
}

internal sealed class MarginFloorRuleConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Pricing.MarginFloorRule>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Pricing.MarginFloorRule> builder) =>
        builder.Property(r => r.FloorAboveNet).HasConversion(new PercentConverter());
}

internal sealed class BurnCapRuleConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Pricing.BurnCapRule>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Pricing.BurnCapRule> builder) =>
        builder.Property(r => r.Cap).HasConversion(new PercentConverter());
}

internal sealed class PartnerSupplierConfiguration : IEntityTypeConfiguration<PartnerSupplier>
{
    public void Configure(EntityTypeBuilder<PartnerSupplier> builder)
    {
        builder.ToTable("PartnerSuppliers");
        builder.HasKey(p => new { p.PartnerId, p.SupplierId });
        builder.Property(p => p.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(p => p.SupplierId).HasConversion(id => id.Value, v => new SupplierId(v));
    }
}

internal sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Ledger.LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Ledger.LedgerAccount> builder)
    {
        builder.ToTable("LedgerAccounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, v => new LedgerAccountId(v));
        builder.Property(a => a.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(a => a.MemberId).HasConversion(
            id => id.HasValue ? id.Value.Value : (Guid?)null,
            v => v.HasValue ? new MemberId(v.Value) : null);
        builder.HasIndex(a => new { a.PartnerId, a.Type })
            .IsUnique()
            .HasFilter("MemberId IS NULL");
        builder.HasIndex(a => new { a.PartnerId, a.MemberId })
            .IsUnique()
            .HasFilter("MemberId IS NOT NULL");
    }
}

internal sealed class LedgerTransactionConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Ledger.LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Ledger.LedgerTransaction> builder)
    {
        builder.ToTable("LedgerTransactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, v => new LedgerTransactionId(v));
        builder.Property(t => t.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(t => t.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(t => t.Reason).HasMaxLength(500).IsRequired();
        builder.Property(t => t.ReversesTransactionId).HasConversion(
            id => id.HasValue ? id.Value.Value : (Guid?)null,
            v => v.HasValue ? new LedgerTransactionId(v.Value) : null);
        builder.Property(t => t.BookingId).HasConversion(
            id => id.HasValue ? id.Value.Value : (Guid?)null,
            v => v.HasValue ? new BookingId(v.Value) : null);
        builder.HasIndex(t => new { t.PartnerId, t.IdempotencyKey }).IsUnique();
        builder.HasIndex(t => new { t.PartnerId, t.OccurredAt });
        builder.OwnsMany(
            t => t.Entries,
            entries =>
            {
                entries.ToTable("LedgerEntries");
                entries.WithOwner().HasForeignKey("TransactionId");
                entries.Property<int>("Id");
                entries.HasKey("Id");
                entries.Property(e => e.AccountId).HasConversion(id => id.Value, v => new LedgerAccountId(v));
                entries.HasIndex(e => e.AccountId);
            });
    }
}

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(r => new { r.PartnerId, r.Operation, r.Key });
        builder.Property(r => r.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(r => r.Operation).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Key).HasMaxLength(128).IsRequired();
        builder.Property(r => r.PayloadHash).HasMaxLength(64).IsRequired();
    }
}

internal sealed class SagaInstanceConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Booking.SagaInstance>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Booking.SagaInstance> builder)
    {
        builder.ToTable("SagaInstances");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, v => new SagaInstanceId(v));
        builder.Property(s => s.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(s => s.BookingId).HasConversion(id => id.Value, v => new BookingId(v));
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(s => s.CorrelationId).HasMaxLength(128).IsRequired();
        builder.Property(s => s.Version).IsConcurrencyToken();
        builder.Ignore(s => s.CompletedSteps);
        builder.Property(s => s.Checkout)
            .HasConversion(PersistenceJson.JsonConverter<LoyaltyLab.Domain.Booking.SagaCheckout>())
            .IsRequired();
        builder.HasIndex(s => s.BookingId).IsUnique();
        builder.HasIndex(s => new { s.Status, s.LastHeartbeatAt });
        builder.OwnsMany(
            s => s.Steps,
            steps =>
            {
                steps.ToTable("SagaSteps");
                steps.WithOwner().HasForeignKey("SagaInstanceId");
                steps.Property<SagaInstanceId>("SagaInstanceId")
                    .HasConversion(id => id.Value, v => new SagaInstanceId(v));
                steps.Property(step => step.Kind).HasConversion<string>().HasMaxLength(32);
                steps.HasKey("SagaInstanceId", nameof(LoyaltyLab.Domain.Booking.SagaStepRecord.Kind));
                steps.Property(step => step.Status).HasConversion<string>().HasMaxLength(32);
                steps.Property(step => step.IdempotencyKey).HasMaxLength(128).IsRequired();
                steps.Property(step => step.ExternalReference).HasMaxLength(128);
                steps.Property(step => step.LastError)
                    .HasConversion(PersistenceJson.NullableJsonConverter<Error>());
                steps.Property(step => step.Compensation)
                    .HasConversion(
                        PersistenceJson.NullableJsonConverter<LoyaltyLab.Domain.Booking.CompensationRecord>());
            });
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Booking.OutboxMessage>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Booking.OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(m => m.Type).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.CorrelationId).HasMaxLength(128).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.Ignore(m => m.IsDispatched);
        builder.HasIndex(m => new { m.DispatchedAt, m.OccurredAt });
    }
}

internal sealed class PoisonMessageConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Booking.PoisonMessage>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Booking.PoisonMessage> builder)
    {
        builder.ToTable("PoisonMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(m => m.Type).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.CorrelationId).HasMaxLength(128).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000).IsRequired();
        builder.HasIndex(m => m.CorrelationId);
    }
}

internal sealed class BusyPeriodConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Opportunity.BusyPeriod>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Opportunity.BusyPeriod> builder)
    {
        builder.ToTable("BusyPeriods");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, v => new BusyPeriodId(v));
        builder.Property(p => p.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(p => p.MemberId).HasConversion(id => id.Value, v => new MemberId(v));
        builder.HasIndex(p => new { p.PartnerId, p.MemberId, p.Start });
    }
}

internal sealed class NudgeConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Opportunity.Nudge>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Opportunity.Nudge> builder)
    {
        builder.ToTable("Nudges");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasConversion(id => id.Value, v => new NudgeId(v));
        builder.Property(n => n.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(n => n.MemberId).HasConversion(id => id.Value, v => new MemberId(v));
        builder.Property(n => n.OfferId).HasConversion(
            id => id.HasValue ? id.Value.Value : (Guid?)null,
            v => v.HasValue ? new OfferId(v.Value) : null);
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(n => n.SuppressedBecause).HasConversion<string>().HasMaxLength(32);
        builder.Property(n => n.Signals)
            .HasConversion(
                PersistenceJson.JsonConverter<List<LoyaltyLab.Domain.Opportunity.OpportunitySignal>>(),
                PersistenceJson.CollectionComparer<List<LoyaltyLab.Domain.Opportunity.OpportunitySignal>>());
        builder.HasIndex(n => new { n.PartnerId, n.MemberId, n.CreatedAt });
    }
}

internal sealed class PriceWatchConfiguration : IEntityTypeConfiguration<LoyaltyLab.Domain.Opportunity.PriceWatch>
{
    public void Configure(EntityTypeBuilder<LoyaltyLab.Domain.Opportunity.PriceWatch> builder)
    {
        builder.ToTable("PriceWatches");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasConversion(id => id.Value, v => new PriceWatchId(v));
        builder.Property(w => w.PartnerId).HasConversion(id => id.Value, v => new PartnerId(v));
        builder.Property(w => w.OfferId).HasConversion(id => id.Value, v => new OfferId(v));
        builder.Property(w => w.BaselineNetRate).HasConversion(new MoneyConverter());
        builder.HasIndex(w => new { w.PartnerId, w.OfferId }).IsUnique();
        builder.HasIndex(w => w.LastCheckedAt);
    }
}
