using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Opportunity;

public enum SignalKind
{
    WindowFit = 0,
    DestinationAffinity = 1,
    TagAffinity = 2,
    CreditCoverage = 3,
    PriceDrop = 4,
}

/// <summary>
/// One named contributor to a nudge score. Contribution is Normalized × Weight so the total is re-derivable (FR-O-04, FR-O-05).
/// </summary>
public sealed record OpportunitySignal
{
    public OpportunitySignal(SignalKind kind, decimal rawValue, decimal normalized, decimal weight, decimal contribution)
    {
        if (normalized is < 0m or > 1m)
        {
            throw new DomainException("A signal's normalized value must be in [0, 1].");
        }

        if (weight is < 0m or > 1m)
        {
            throw new DomainException("A signal weight must be in [0, 1].");
        }

        if (contribution != normalized * weight)
        {
            throw new DomainException("Contribution must equal Normalized × Weight.");
        }

        Kind = kind;
        RawValue = rawValue;
        Normalized = normalized;
        Weight = weight;
        Contribution = contribution;
    }

    public SignalKind Kind { get; }

    public decimal RawValue { get; }

    public decimal Normalized { get; }

    public decimal Weight { get; }

    public decimal Contribution { get; }

    public static OpportunitySignal Of(SignalKind kind, decimal rawValue, decimal normalized, decimal weight) =>
        new(kind, rawValue, normalized, weight, normalized * weight);
}
