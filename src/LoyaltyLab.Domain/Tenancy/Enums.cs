namespace LoyaltyLab.Domain.Tenancy;

public enum AccessRole
{
    Anonymous = 0,
    Member = 1,
    AccountManager = 2,
    FinanceAnalyst = 3,
    Operator = 4,
}

public enum TierCode
{
    Standard = 0,
    Silver = 1,
    Gold = 2,
}

public enum RateDriftPolicy
{
    AbsorbWithinTolerance = 0,
    RequoteRequired = 1,
}
