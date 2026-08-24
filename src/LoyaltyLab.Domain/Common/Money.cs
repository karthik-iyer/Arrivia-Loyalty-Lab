namespace LoyaltyLab.Domain.Common;

/// <summary>
/// A monetary amount with an explicit currency. Arithmetic across currencies throws.
/// Intermediate operations retain full decimal precision; only <see cref="RoundToCents"/> rounds.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }

    public Currency Currency { get; }

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money Of(decimal amount, Currency currency) => new(amount, currency);

    public bool IsNegative => Amount < 0m;

    public bool IsZero => Amount == 0m;

    public Money Add(Money other) => this + other;

    public Money Subtract(Money other) => this - other;

    public Money Multiply(decimal factor) => this * factor;

    /// <summary>
    /// Applies a signed percent to the amount with no rounding.
    /// +12% of 100.00 is 112.00; −3% of 128.80 is 124.936.
    /// </summary>
    public Money ApplyPercent(Percent percent) => new(Amount * (1m + percent.AsFraction()), Currency);

    /// <summary>
    /// The only rounding the domain is allowed to perform. Two decimal places, away from zero at midpoint.
    /// </summary>
    public Money RoundToCents() =>
        new(decimal.Round(Amount, 2, MidpointRounding.AwayFromZero), Currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money left, decimal factor) => new(left.Amount * factor, left.Currency);

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Amount} {Currency.Code}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new DomainException(
                $"Cannot combine {left.Currency.Code} with {right.Currency.Code}. " +
                "Cross-currency arithmetic is a defect; convert explicitly first.");
        }
    }
}
