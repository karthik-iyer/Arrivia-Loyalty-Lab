using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Tenancy;

/// <summary>
/// Partner-tunable knobs. Changing any of these is configuration, not a deployment (FR-X-07).
/// </summary>
public sealed record CreditPolicy
{
    public CreditPolicy(decimal creditUnitValue, Percent defaultBurnCap, int creditLifetimeDays, Percent earnRateOnMargin)
    {
        if (creditUnitValue <= 0m)
        {
            throw new DomainException("CreditUnitValue must be positive so conversion to money is lossless and defined.");
        }

        if (defaultBurnCap.Value is < 0m or > 100m)
        {
            throw new DomainException("DefaultBurnCap must be between 0 and 100 percent.");
        }

        if (creditLifetimeDays <= 0)
        {
            throw new DomainException("CreditLifetimeDays must be positive.");
        }

        CreditUnitValue = creditUnitValue;
        DefaultBurnCap = defaultBurnCap;
        CreditLifetimeDays = creditLifetimeDays;
        EarnRateOnMargin = earnRateOnMargin;
    }

    public decimal CreditUnitValue { get; }

    public Percent DefaultBurnCap { get; }

    public int CreditLifetimeDays { get; }

    public Percent EarnRateOnMargin { get; }

    public int ToCredits(Money amount)
    {
        if (amount.IsNegative)
        {
            throw new DomainException("Cannot convert a negative amount to credits.");
        }

        return (int)decimal.Round(amount.Amount / CreditUnitValue, 0, MidpointRounding.AwayFromZero);
    }

    public Money ToMoney(int credits, Currency currency)
    {
        if (credits < 0)
        {
            throw new DomainException("Cannot convert a negative credit balance to money.");
        }

        return Money.Of(credits * CreditUnitValue, currency);
    }
}

public sealed record QuotePolicy
{
    public QuotePolicy(int validityMinutes, RateDriftPolicy driftPolicy, Percent driftTolerance)
    {
        if (validityMinutes <= 0)
        {
            throw new DomainException("Quote validity must be a positive number of minutes.");
        }

        if (driftTolerance.Value < 0m)
        {
            throw new DomainException("DriftTolerance cannot be negative.");
        }

        ValidityMinutes = validityMinutes;
        DriftPolicy = driftPolicy;
        DriftTolerance = driftTolerance;
    }

    public int ValidityMinutes { get; }

    public RateDriftPolicy DriftPolicy { get; }

    public Percent DriftTolerance { get; }
}

public sealed record SagaPolicy
{
    public SagaPolicy(int stepTimeoutSeconds, int maxStepAttempts, int maxCompensationAttempts, int stalledAfterSeconds)
    {
        if (stepTimeoutSeconds <= 0 || maxStepAttempts <= 0 || maxCompensationAttempts <= 0 || stalledAfterSeconds <= 0)
        {
            throw new DomainException("Saga policy values must be positive.");
        }

        StepTimeoutSeconds = stepTimeoutSeconds;
        MaxStepAttempts = maxStepAttempts;
        MaxCompensationAttempts = maxCompensationAttempts;
        StalledAfterSeconds = stalledAfterSeconds;
    }

    public int StepTimeoutSeconds { get; }

    public int MaxStepAttempts { get; }

    public int MaxCompensationAttempts { get; }

    public int StalledAfterSeconds { get; }
}

/// <summary>
/// Relative weights for opportunity scoring. Must sum to 1 so a score is a convex combination.
/// </summary>
public sealed record SignalWeights
{
    public SignalWeights(
        decimal windowFit,
        decimal destinationAffinity,
        decimal tagAffinity,
        decimal creditCoverage,
        decimal priceDrop)
    {
        if (windowFit < 0m || destinationAffinity < 0m || tagAffinity < 0m || creditCoverage < 0m || priceDrop < 0m)
        {
            throw new DomainException("Signal weights cannot be negative.");
        }

        var sum = windowFit + destinationAffinity + tagAffinity + creditCoverage + priceDrop;
        if (sum != 1m)
        {
            throw new DomainException($"Signal weights must sum to 1.0, not {sum}.");
        }

        WindowFit = windowFit;
        DestinationAffinity = destinationAffinity;
        TagAffinity = tagAffinity;
        CreditCoverage = creditCoverage;
        PriceDrop = priceDrop;
    }

    public decimal WindowFit { get; }

    public decimal DestinationAffinity { get; }

    public decimal TagAffinity { get; }

    public decimal CreditCoverage { get; }

    public decimal PriceDrop { get; }
}

public sealed record OpportunityPolicy
{
    public OpportunityPolicy(
        int minWindowNights,
        int minLeadDays,
        decimal scoreThreshold,
        Percent priceDropThreshold,
        int maxNudgesPerMemberPerWeek,
        int dismissalCooldownDays,
        int nudgeLifetimeDays,
        SignalWeights weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        if (minWindowNights <= 0 || minLeadDays < 0)
        {
            throw new DomainException("Travel-window thresholds are invalid.");
        }

        if (scoreThreshold is < 0m or > 1m)
        {
            throw new DomainException("ScoreThreshold must be in [0, 1].");
        }

        if (maxNudgesPerMemberPerWeek <= 0 || dismissalCooldownDays < 0 || nudgeLifetimeDays <= 0)
        {
            throw new DomainException("Fatigue and lifetime settings are invalid.");
        }

        MinWindowNights = minWindowNights;
        MinLeadDays = minLeadDays;
        ScoreThreshold = scoreThreshold;
        PriceDropThreshold = priceDropThreshold;
        MaxNudgesPerMemberPerWeek = maxNudgesPerMemberPerWeek;
        DismissalCooldownDays = dismissalCooldownDays;
        NudgeLifetimeDays = nudgeLifetimeDays;
        Weights = weights;
    }

    public int MinWindowNights { get; }

    public int MinLeadDays { get; }

    public decimal ScoreThreshold { get; }

    public Percent PriceDropThreshold { get; }

    public int MaxNudgesPerMemberPerWeek { get; }

    public int DismissalCooldownDays { get; }

    public int NudgeLifetimeDays { get; }

    public SignalWeights Weights { get; }
}
