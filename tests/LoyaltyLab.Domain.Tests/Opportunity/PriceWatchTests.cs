using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Opportunity;

namespace LoyaltyLab.Domain.Tests.Opportunity;

public sealed class PriceWatchTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordCheck_rolls_the_baseline_to_the_live_net()
    {
        var watch = PriceWatch.Open(
            PartnerId.New(),
            OfferId.New(),
            Money.Of(115m, Currency.Usd),
            new MutableClock(AsOf));

        watch.RecordCheck(Money.Of(100m, Currency.Usd), new MutableClock(AsOf.AddDays(14)));

        watch.BaselineNetRate.Amount.Should().Be(100m);
        watch.LastCheckedAt.Should().Be(AsOf.AddDays(14));
    }

    [Fact]
    public void RecordCheck_rejects_a_currency_mismatch()
    {
        var watch = PriceWatch.Open(
            PartnerId.New(),
            OfferId.New(),
            Money.Of(100m, Currency.Usd),
            new MutableClock(AsOf));

        var act = () => watch.RecordCheck(Money.Of(100m, Currency.Of("EUR")), new MutableClock(AsOf));

        act.Should().Throw<DomainException>();
    }
}
