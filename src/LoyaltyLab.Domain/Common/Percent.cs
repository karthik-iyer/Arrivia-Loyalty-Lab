namespace LoyaltyLab.Domain.Common;

/// <summary>
/// A percentage with an explicit sign: +12 is a twelve percent increase, −3 a three percent reduction.
/// Conversion to a fraction is the only arithmetic this type performs; rounding is never applied here.
/// </summary>
public readonly record struct Percent
{
    public decimal Value { get; }

    private Percent(decimal value) => Value = value;

    public static Percent From(decimal value) => new(value);

    public static Percent Zero { get; } = new(0m);

    /// <summary>12% → 0.12. Used by <see cref="Money.ApplyPercent"/>; does not round.</summary>
    public decimal AsFraction() => Value / 100m;

    public override string ToString() => $"{Value}%";
}
