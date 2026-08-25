using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Concierge;

/// <summary>
/// Documented ranking mix (docs/04 §5.1). Must sum to 1 so a score is a convex combination.
/// </summary>
public sealed record RankingWeights
{
    public RankingWeights(
        decimal valueForMoney,
        decimal creditCoverage,
        decimal tagMatch,
        decimal starRating)
    {
        if (valueForMoney < 0m || creditCoverage < 0m || tagMatch < 0m || starRating < 0m)
        {
            throw new DomainException("Ranking weights cannot be negative.");
        }

        var sum = valueForMoney + creditCoverage + tagMatch + starRating;
        if (sum != 1m)
        {
            throw new DomainException($"Ranking weights must sum to 1.0, not {sum}.");
        }

        ValueForMoney = valueForMoney;
        CreditCoverage = creditCoverage;
        TagMatch = tagMatch;
        StarRating = starRating;
    }

    public static RankingWeights Default { get; } = new(0.40m, 0.25m, 0.20m, 0.15m);

    public decimal ValueForMoney { get; }

    public decimal CreditCoverage { get; }

    public decimal TagMatch { get; }

    public decimal StarRating { get; }
}
