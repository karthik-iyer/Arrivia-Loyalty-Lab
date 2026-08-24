using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Tenancy;

public sealed class PartnerAndMemberTests
{
    [Fact]
    public void Partner_code_is_normalized_to_uppercase()
    {
        Fixtures.Summit().Code.Should().Be("SUMMIT");
    }

    [Fact]
    public void Partner_rejects_a_missing_code()
    {
        var act = () => Partner.Create(
            " ",
            "Summit",
            Currency.Usd,
            Fixtures.Theme,
            Fixtures.Credits,
            Fixtures.Quotes,
            Fixtures.Sagas,
            Fixtures.Opportunities);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Member_belongs_to_exactly_one_partner()
    {
        var partner = Fixtures.Summit();
        var maya = Member.Create(partner.Id, "Maya", TierCode.Gold);

        maya.PartnerId.Should().Be(partner.Id);
        maya.Tier.Should().Be(TierCode.Gold);
        maya.IsActive.Should().BeTrue();
    }

    [Fact]
    public void TenantContext_for_a_member_carries_tier_and_role()
    {
        var partner = Fixtures.Summit();
        var maya = Member.Create(partner.Id, "Maya", TierCode.Gold);

        var context = TenantContext.ForMember(maya);

        context.Role.Should().Be(AccessRole.Member);
        context.MemberId.Should().Be(maya.Id);
        context.Tier.Should().Be(TierCode.Gold);
        context.HasMember.Should().BeTrue();
    }

    [Fact]
    public void Anonymous_context_has_a_partner_but_no_member()
    {
        var partner = Fixtures.Summit();
        var context = TenantContext.Anonymous(partner.Id);

        context.Role.Should().Be(AccessRole.Anonymous);
        context.MemberId.Should().BeNull();
        context.HasMember.Should().BeFalse();
    }

    [Fact]
    public void Member_role_without_a_member_id_is_rejected()
    {
        var act = () => new TenantContext(PartnerId.New(), memberId: null, tier: null, AccessRole.Member);

        act.Should().Throw<DomainException>();
    }
}

public sealed class PolicyTests
{
    [Fact]
    public void Credit_unit_value_must_be_positive()
    {
        var act = () => new CreditPolicy(0m, Percent.From(40m), 730, Percent.From(10m));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Signal_weights_must_sum_to_one()
    {
        var act = () => new SignalWeights(0.5m, 0.5m, 0.5m, 0m, 0m);

        act.Should().Throw<DomainException>().WithMessage("*1.0*");
    }

    [Fact]
    public void Theme_requires_hex_colours()
    {
        var act = () => new PartnerTheme("pink", "#FFFFFF", "#000000");

        act.Should().Throw<DomainException>().WithMessage("*#RRGGBB*");
    }
}

public sealed class CatalogTests
{
    [Fact]
    public void Offer_keeps_net_rate_as_domain_data()
    {
        var supplier = Supplier.Create("OCEANIC", "Oceanic Hotels");
        var offer = Fixtures.Offer(supplier.Id);

        offer.NetRate.Amount.Should().Be(100.00m);
        offer.Tags.Should().Contain(OfferTag.Beach);
        offer.StarRating.Should().Be(4);
    }

    [Fact]
    public void Offer_rejects_mismatched_rate_currencies()
    {
        var act = () => TravelOffer.Create(
            SupplierId.New(),
            "X",
            new Destination("MBJ", "Montego Bay"),
            Money.Of(100m, Currency.Usd),
            Money.Of(15m, Currency.Of("EUR")),
            [OfferTag.Beach],
            4,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        act.Should().Throw<DomainException>().WithMessage("*same currency*");
    }

    [Fact]
    public void Offer_rejects_an_inverted_availability_window()
    {
        var act = () => TravelOffer.Create(
            SupplierId.New(),
            "X",
            new Destination("MBJ", "Montego Bay"),
            Money.Of(100m, Currency.Usd),
            Money.Of(15m, Currency.Usd),
            [OfferTag.City],
            3,
            new DateOnly(2026, 12, 31),
            new DateOnly(2026, 1, 1));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Star_rating_must_be_between_one_and_five()
    {
        var act = () => TravelOffer.Create(
            SupplierId.New(),
            "X",
            new Destination("MBJ", "Montego Bay"),
            Money.Of(100m, Currency.Usd),
            Money.Of(15m, Currency.Usd),
            [OfferTag.City],
            0,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        act.Should().Throw<DomainException>();
    }
}
