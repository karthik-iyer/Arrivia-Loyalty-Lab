using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Tests.Pricing;

public sealed class QuoteTests
{
    [Fact]
    public void Quote_expires_at_the_policy_window()
    {
        var (quote, _, clock) = IssueSummitQuote();

        quote.IsExpired(clock).Should().BeFalse();
        clock.UtcNow = quote.ExpiresAt;
        quote.IsExpired(clock).Should().BeTrue();
    }

    [Fact]
    public void Expired_quote_cannot_be_revalidated()
    {
        var (quote, offer, clock) = IssueSummitQuote();
        clock.UtcNow = quote.ExpiresAt;

        var result = quote.Revalidate(offer, Fixtures.Quotes, Percent.From(5m), clock);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.QuoteExpired);
    }

    [Fact]
    public void Unchanged_net_rate_is_accepted()
    {
        var (quote, offer, clock) = IssueNimbusQuote();

        var result = quote.Revalidate(offer, Fixtures.Quotes, Percent.From(5m), clock);

        result.IsSuccess.Should().BeTrue();
        result.Value.Kind.Should().Be(RateDriftKind.Unchanged);
    }

    [Fact]
    public void Absorb_policy_accepts_drift_within_tolerance_when_floor_still_holds()
    {
        var (quote, offer, clock) = IssueNimbusQuote();
        var drifted = Clone(offer, net: 101.00m);

        var result = quote.Revalidate(drifted, Fixtures.Quotes, Percent.From(5m), clock);

        result.IsSuccess.Should().BeTrue();
        result.Value.Kind.Should().Be(RateDriftKind.Absorbed);
        result.Value.NetRateDelta!.Value.Amount.Should().Be(1.00m);
    }

    [Fact]
    public void Absorb_policy_rejects_when_the_floor_would_break()
    {
        var (quote, offer, clock) = IssueSummitQuote();
        var drifted = Clone(offer, net: 101.00m);

        var result = quote.Revalidate(drifted, Fixtures.Quotes, Percent.From(5m), clock);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.RateChanged);
    }

    [Fact]
    public void Requote_policy_rejects_any_rate_change()
    {
        var (quote, offer, clock) = IssueNimbusQuote();
        var drifted = Clone(offer, net: 100.50m);
        var policy = new QuotePolicy(15, RateDriftPolicy.RequoteRequired, Percent.From(2m));

        var result = quote.Revalidate(drifted, policy, Percent.From(5m), clock);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.RateChanged);
    }

    [Fact]
    public void Drift_beyond_tolerance_is_rejected()
    {
        var (quote, offer, clock) = IssueNimbusQuote();
        var drifted = Clone(offer, net: 103.00m);

        var result = quote.Revalidate(drifted, Fixtures.Quotes, Percent.From(5m), clock);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.RateChanged);
    }

    private static (Quote Quote, TravelOffer Offer, MutableClock Clock) IssueSummitQuote()
    {
        var partner = Fixtures.Summit();
        var member = Member.Create(partner.Id, "Maya", TierCode.Gold);
        var offer = PricingExamples.OceanicBeachOffer();
        var state = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner.Id, offer, TierCode.Gold, PricingExamples.SummitRules(partner.Id)));
        var clock = new MutableClock(PricingExamples.AsOf);
        var quote = Quote.Create(member, offer, state, partner.QuotePolicy, clock);
        return (quote, offer, clock);
    }

    private static (Quote Quote, TravelOffer Offer, MutableClock Clock) IssueNimbusQuote()
    {
        var partner = Partner.Create(
            "NIMBUS",
            "Nimbus Club",
            Currency.Usd,
            Fixtures.Theme,
            new CreditPolicy(0.01m, Percent.From(100m), 730, Percent.From(10m)),
            Fixtures.Quotes,
            Fixtures.Sagas,
            Fixtures.Opportunities);
        var member = Member.Create(partner.Id, "Chen", TierCode.Standard);
        var offer = PricingExamples.OceanicBeachOffer();
        var state = PricingExamples.Pipeline.Execute(
            PricingExamples.Request(partner.Id, offer, tier: null, PricingExamples.NimbusRules(partner.Id)));
        var clock = new MutableClock(PricingExamples.AsOf);
        var quote = Quote.Create(member, offer, state, partner.QuotePolicy, clock);
        return (quote, offer, clock);
    }

    private static TravelOffer Clone(TravelOffer offer, decimal net) =>
        TravelOffer.Create(
            offer.SupplierId,
            offer.PropertyName,
            offer.Destination,
            Money.Of(net, offer.NetRate.Currency),
            offer.TaxesAndFees,
            offer.Tags,
            offer.StarRating,
            offer.AvailableFrom,
            offer.AvailableTo,
            offer.Id);
}
