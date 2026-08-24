using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
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
        builder.Property(o => o.Tags).HasConversion(PersistenceJson.JsonConverter<HashSet<OfferTag>>());
        builder.HasIndex(o => new { o.SupplierId, o.AvailableFrom, o.AvailableTo });
    }
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
