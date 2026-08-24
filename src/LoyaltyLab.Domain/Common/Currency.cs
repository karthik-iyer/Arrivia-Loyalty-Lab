namespace LoyaltyLab.Domain.Common;

/// <summary>
/// ISO 4217 currency. Cross-currency arithmetic is a defect, not a conversion.
/// </summary>
public readonly record struct Currency
{
    public string Code { get; }

    private Currency(string code) => Code = code;

    public static Currency Usd { get; } = new("USD");

    public static Currency Of(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 3)
        {
            throw new DomainException("Currency code must be a 3-letter ISO 4217 code.");
        }

        return new Currency(code.Trim().ToUpperInvariant());
    }

    public override string ToString() => Code;
}
