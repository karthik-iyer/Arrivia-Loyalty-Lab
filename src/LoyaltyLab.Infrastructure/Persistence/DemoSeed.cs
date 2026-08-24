using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;
using LoyaltyLab.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Infrastructure.Persistence;

/// <summary>
/// Well-known identifiers so the demo script and tests address the same rows on every machine.
/// </summary>
public static class SeedIds
{
    public static PartnerId Summit { get; } = new(Guid.Parse("a11ce001-0001-7000-8000-000000000001"));

    public static PartnerId Nimbus { get; } = new(Guid.Parse("a11ce001-0001-7000-8000-000000000002"));

    public static MemberId Maya { get; } = new(Guid.Parse("a11ce001-0002-7000-8000-000000000001"));

    public static MemberId Ravi { get; } = new(Guid.Parse("a11ce001-0002-7000-8000-000000000002"));

    public static MemberId Chen { get; } = new(Guid.Parse("a11ce001-0002-7000-8000-000000000003"));

    public static SupplierId Oceanic { get; } = new(Guid.Parse("a11ce001-0003-7000-8000-000000000001"));

    public static SupplierId Alpine { get; } = new(Guid.Parse("a11ce001-0003-7000-8000-000000000002"));

    public static SupplierId Metro { get; } = new(Guid.Parse("a11ce001-0003-7000-8000-000000000003"));

    public static OfferId Offer(int index) =>
        new(Guid.Parse($"a11ce001-0004-7000-8000-{index:D12}"));

    public static PricingRuleId Rule(int index) =>
        new(Guid.Parse($"a11ce001-0005-7000-8000-{index:D12}"));
}

/// <summary>
/// Idempotent demo catalog from docs/04 §8.3, including opening ledger grants.
/// </summary>
public static class DemoSeed
{
    public static async Task EnsureAsync(LoyaltyLabDbContext db, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (!await db.Partners.AnyAsync(p => p.Id == SeedIds.Summit, cancellationToken))
        {
            db.Partners.Add(CreateSummit());
            db.Partners.Add(CreateNimbus());
        }

        if (!await db.Suppliers.AnyAsync(s => s.Id == SeedIds.Oceanic, cancellationToken))
        {
            db.Suppliers.Add(Supplier.Create("OCEANIC", "Oceanic Hotels", SeedIds.Oceanic));
            db.Suppliers.Add(Supplier.Create("ALPINE", "Alpine Lodges", SeedIds.Alpine));
            db.Suppliers.Add(Supplier.Create("METRO", "Metro Stays", SeedIds.Metro));
        }

        if (!await db.Members.IgnoreQueryFilters().AnyAsync(m => m.Id == SeedIds.Maya, cancellationToken))
        {
            db.Members.Add(Member.Create(SeedIds.Summit, "Maya", TierCode.Gold, id: SeedIds.Maya));
            db.Members.Add(Member.Create(SeedIds.Summit, "Ravi", TierCode.Standard, id: SeedIds.Ravi));
            db.Members.Add(Member.Create(SeedIds.Nimbus, "Chen", TierCode.Standard, id: SeedIds.Chen));
        }

        if (!await db.PartnerSuppliers.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            db.PartnerSuppliers.Add(PartnerSupplier.Permit(SeedIds.Summit, SeedIds.Oceanic));
            db.PartnerSuppliers.Add(PartnerSupplier.Permit(SeedIds.Summit, SeedIds.Alpine));
            db.PartnerSuppliers.Add(PartnerSupplier.Permit(SeedIds.Summit, SeedIds.Metro));
            db.PartnerSuppliers.Add(PartnerSupplier.Permit(SeedIds.Nimbus, SeedIds.Alpine));
            db.PartnerSuppliers.Add(PartnerSupplier.Permit(SeedIds.Nimbus, SeedIds.Metro));
        }

        if (!await db.Offers.AnyAsync(cancellationToken))
        {
            db.Offers.AddRange(CreateOffers());
        }

        if (!await db.PricingRules.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            db.PricingRules.AddRange(CreateRules());
        }

        if (!await db.LedgerTransactions.IgnoreQueryFilters()
                .AnyAsync(transaction => transaction.IdempotencyKey == "seed-earn-maya", cancellationToken))
        {
            SeedOpeningLedger(db);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void SeedOpeningLedger(LoyaltyLabDbContext db)
    {
        var clock = new FixedDemoClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var summitIssuance = LedgerAccount.Issuance(SeedIds.Summit);
        var nimbusIssuance = LedgerAccount.Issuance(SeedIds.Nimbus);
        var maya = LedgerAccount.MemberCredits(SeedIds.Summit, SeedIds.Maya);
        var ravi = LedgerAccount.MemberCredits(SeedIds.Summit, SeedIds.Ravi);
        var chen = LedgerAccount.MemberCredits(SeedIds.Nimbus, SeedIds.Chen);

        db.LedgerAccounts.AddRange(summitIssuance, nimbusIssuance, maya, ravi, chen);
        db.LedgerTransactions.AddRange(
            LedgerTransaction.Earn(maya, summitIssuance, 6_000, "seed-earn-maya", "Opening grant", clock),
            LedgerTransaction.Earn(ravi, summitIssuance, 500, "seed-earn-ravi", "Opening grant", clock),
            LedgerTransaction.Earn(chen, nimbusIssuance, 12_000, "seed-earn-chen", "Opening grant", clock));
    }

    private static Partner CreateSummit() =>
        Partner.Create(
            "SUMMIT",
            "Summit Rewards",
            Currency.Usd,
            new PartnerTheme("#BE185D", "#FFF7ED", "#1D4ED8"),
            new CreditPolicy(0.01m, Percent.From(40m), 730, Percent.From(10m)),
            new QuotePolicy(15, RateDriftPolicy.AbsorbWithinTolerance, Percent.From(2m)),
            SharedSaga,
            SharedOpportunity,
            SeedIds.Summit);

    private static Partner CreateNimbus() =>
        Partner.Create(
            "NIMBUS",
            "Nimbus Club",
            Currency.Usd,
            new PartnerTheme("#0F766E", "#F0FDFA", "#134E4A"),
            new CreditPolicy(0.01m, Percent.From(100m), 730, Percent.From(10m)),
            new QuotePolicy(15, RateDriftPolicy.RequoteRequired, Percent.From(2m)),
            SharedSaga,
            SharedOpportunity,
            SeedIds.Nimbus);

    private static SagaPolicy SharedSaga { get; } = new(10, 3, 5, 60);

    private static OpportunityPolicy SharedOpportunity { get; } = new(
        3, 14, 0.55m, Percent.From(10m), 2, 30, 7,
        new SignalWeights(0.2m, 0.2m, 0.2m, 0.2m, 0.2m));

    private static List<TravelOffer> CreateOffers()
    {
        var winter = (new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        var summer = (new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31));
        var mbj = new Destination("MBJ", "Montego Bay");
        var zrh = new Destination("ZRH", "Zermatt");
        var nyc = new Destination("NYC", "New York");

        var rows = new (SupplierId Supplier, string Name, Destination Destination, OfferTag[] Tags, int Stars, decimal Net, decimal Tax)[]
        {
            (SeedIds.Oceanic, "Coral Bay Resort", mbj, [OfferTag.Beach, OfferTag.Family], 4, 100m, 15m),
            (SeedIds.Oceanic, "Palms at Negril", mbj, [OfferTag.Beach], 3, 85m, 12m),
            (SeedIds.Oceanic, "Blue Lagoon Villas", mbj, [OfferTag.Beach, OfferTag.Luxury], 5, 220m, 28m),
            (SeedIds.Oceanic, "Sandpiper Suites", mbj, [OfferTag.Beach, OfferTag.Family], 4, 130m, 18m),
            (SeedIds.Oceanic, "Rum Cove Inn", mbj, [OfferTag.Beach], 3, 75m, 10m),
            (SeedIds.Oceanic, "Orchid Reef Hotel", mbj, [OfferTag.Beach, OfferTag.Luxury], 5, 260m, 32m),
            (SeedIds.Oceanic, "Calypso House", mbj, [OfferTag.Beach, OfferTag.Family], 4, 145m, 20m),
            (SeedIds.Oceanic, "Tradewinds Lodge", mbj, [OfferTag.Beach], 3, 95m, 14m),
            (SeedIds.Alpine, "Matterhorn Lodge", zrh, [OfferTag.Ski, OfferTag.Family], 4, 180m, 22m),
            (SeedIds.Alpine, "Glacier Peak Inn", zrh, [OfferTag.Ski], 3, 120m, 16m),
            (SeedIds.Alpine, "Alpine Crown", zrh, [OfferTag.Ski, OfferTag.Luxury], 5, 310m, 40m),
            (SeedIds.Alpine, "Pinecrest Chalet", zrh, [OfferTag.Ski, OfferTag.Family], 4, 165m, 20m),
            (SeedIds.Alpine, "Nordic Loft", zrh, [OfferTag.Ski], 3, 110m, 14m),
            (SeedIds.Alpine, "Summit Spa Residences", zrh, [OfferTag.Ski, OfferTag.Luxury], 5, 340m, 42m),
            (SeedIds.Alpine, "Valley Hearth Hotel", zrh, [OfferTag.Ski, OfferTag.Family], 4, 150m, 18m),
            (SeedIds.Alpine, "Icefall Cabin", zrh, [OfferTag.Ski], 3, 98m, 12m),
            (SeedIds.Metro, "Hudson House", nyc, [OfferTag.City], 4, 190m, 24m),
            (SeedIds.Metro, "Chelsea Walk Hotel", nyc, [OfferTag.City, OfferTag.Family], 3, 140m, 18m),
            (SeedIds.Metro, "Fifth Avenue Atelier", nyc, [OfferTag.City, OfferTag.Luxury], 5, 380m, 48m),
            (SeedIds.Metro, "Brooklyn Bridge Inn", nyc, [OfferTag.City, OfferTag.Family], 4, 160m, 20m),
            (SeedIds.Metro, "Soho Lantern", nyc, [OfferTag.City], 3, 125m, 16m),
            (SeedIds.Metro, "Central Park Residences", nyc, [OfferTag.City, OfferTag.Luxury], 5, 410m, 52m),
            (SeedIds.Metro, "Harlem Porch Hotel", nyc, [OfferTag.City, OfferTag.Family], 4, 135m, 17m),
            (SeedIds.Metro, "Queens Landing", nyc, [OfferTag.City], 3, 105m, 14m),
        };

        return rows.Select((row, index) =>
        {
            var window = index % 2 == 0 ? winter : summer;
            return TravelOffer.Create(
                row.Supplier,
                row.Name,
                row.Destination,
                Money.Of(row.Net, Currency.Usd),
                Money.Of(row.Tax, Currency.Usd),
                row.Tags,
                row.Stars,
                window.Item1,
                window.Item2,
                SeedIds.Offer(index + 1));
        }).ToList();
    }

    private static List<PricingRule> CreateRules()
    {
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        return
        [
            BaseMarkupRule.Create(
                SeedIds.Summit, Percent.From(12m), RuleScope.PartnerWide, from, id: SeedIds.Rule(1)),
            TierAdjustmentRule.Create(
                SeedIds.Summit, Percent.From(-3m), new RuleScope(tier: TierCode.Gold), from, id: SeedIds.Rule(2)),
            CampaignDiscountRule.Create(
                SeedIds.Summit,
                "MARCH-BEACH",
                Percent.From(-5m),
                new RuleScope(tag: OfferTag.Beach),
                from,
                id: SeedIds.Rule(3)),
            MarginFloorRule.Create(
                SeedIds.Summit, Percent.From(5m), RuleScope.PartnerWide, from, id: SeedIds.Rule(4)),
            BurnCapRule.Create(
                SeedIds.Summit, Percent.From(40m), RuleScope.PartnerWide, from, id: SeedIds.Rule(5)),
            BaseMarkupRule.Create(
                SeedIds.Nimbus, Percent.From(18m), RuleScope.PartnerWide, from, id: SeedIds.Rule(6)),
            MarginFloorRule.Create(
                SeedIds.Nimbus, Percent.From(5m), RuleScope.PartnerWide, from, id: SeedIds.Rule(7)),
            BurnCapRule.Create(
                SeedIds.Nimbus, Percent.From(100m), RuleScope.PartnerWide, from, id: SeedIds.Rule(8)),
        ];
    }
}
