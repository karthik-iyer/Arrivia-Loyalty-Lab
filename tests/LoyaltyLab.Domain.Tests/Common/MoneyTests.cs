using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Tests.Common;

public sealed class MoneyTests
{
    private static readonly Currency Usd = Currency.Usd;
    private static readonly Currency Eur = Currency.Of("EUR");

    [Fact]
    public void Of_preserves_full_precision()
    {
        Money.Of(124.936m, Usd).Amount.Should().Be(124.936m);
    }

    [Fact]
    public void Zero_is_zero_in_the_given_currency()
    {
        var zero = Money.Zero(Usd);

        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be(Usd);
        zero.IsZero.Should().BeTrue();
        zero.IsNegative.Should().BeFalse();
    }

    [Fact]
    public void Adding_the_same_currency_sums_amounts()
    {
        Money.Of(100.00m, Usd).Add(Money.Of(15.00m, Usd)).Amount.Should().Be(115.00m);
    }

    [Fact]
    public void Adding_different_currencies_throws()
    {
        var act = () => Money.Of(1m, Usd) + Money.Of(1m, Eur);

        act.Should().Throw<DomainException>().WithMessage("*USD*EUR*");
    }

    [Fact]
    public void Subtracting_different_currencies_throws()
    {
        var act = () => Money.Of(1m, Usd) - Money.Of(1m, Eur);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Comparing_different_currencies_throws()
    {
        var act = () => Money.Of(1m, Usd).CompareTo(Money.Of(1m, Eur));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ApplyPercent_does_not_round()
    {
        // Worked example: 128.80 with Gold −3% stays 124.936, not 124.94.
        var markedUp = Money.Of(128.80m, Usd);

        markedUp.ApplyPercent(Percent.From(-3m)).Amount.Should().Be(124.936m);
    }

    [Fact]
    public void ApplyPercent_increase_is_exact()
    {
        Money.Of(100.00m, Usd).ApplyPercent(Percent.From(12m)).Amount.Should().Be(112.00m);
    }

    [Fact]
    public void RoundToCents_uses_away_from_zero()
    {
        Money.Of(1.225m, Usd).RoundToCents().Amount.Should().Be(1.23m);
        Money.Of(1.224m, Usd).RoundToCents().Amount.Should().Be(1.22m);
        Money.Of(-1.225m, Usd).RoundToCents().Amount.Should().Be(-1.23m);
    }

    [Fact]
    public void Multiply_does_not_round()
    {
        (Money.Of(100.00m, Usd) * 1.185m).Amount.Should().Be(118.5m);
    }

    [Fact]
    public void IsNegative_follows_the_sign()
    {
        Money.Of(-0.01m, Usd).IsNegative.Should().BeTrue();
        Money.Of(0.00m, Usd).IsNegative.Should().BeFalse();
    }

    [Fact]
    public void Comparison_orders_by_amount_when_currency_matches()
    {
        (Money.Of(1m, Usd) < Money.Of(2m, Usd)).Should().BeTrue();
        (Money.Of(2m, Usd) >= Money.Of(2m, Usd)).Should().BeTrue();
    }
}
