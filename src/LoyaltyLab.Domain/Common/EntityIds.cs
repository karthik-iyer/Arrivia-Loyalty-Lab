namespace LoyaltyLab.Domain.Common;

/// <summary>
/// Strongly typed identifiers. Using a dedicated type per aggregate makes mixing a partner id
/// with a member id a compile error rather than a runtime surprise.
/// </summary>
public static class EntityIds
{
    public static Guid NewValue() => Guid.CreateVersion7();
}

public readonly record struct PartnerId(Guid Value)
{
    public static PartnerId New() => new(EntityIds.NewValue());

    public override string ToString() => Value.ToString();
}

public readonly record struct MemberId(Guid Value)
{
    public static MemberId New() => new(EntityIds.NewValue());

    public override string ToString() => Value.ToString();
}

public readonly record struct SupplierId(Guid Value)
{
    public static SupplierId New() => new(EntityIds.NewValue());

    public override string ToString() => Value.ToString();
}

public readonly record struct OfferId(Guid Value)
{
    public static OfferId New() => new(EntityIds.NewValue());

    public override string ToString() => Value.ToString();
}

public readonly record struct PricingRuleId(Guid Value)
{
    public static PricingRuleId New() => new(EntityIds.NewValue());

    public override string ToString() => Value.ToString();
}

public readonly record struct QuoteId(Guid Value)
{
    public static QuoteId New() => new(EntityIds.NewValue());

    public override string ToString() => Value.ToString();
}

public readonly record struct BookingId(Guid Value)
{
    public static BookingId New() => new(EntityIds.NewValue());

    public override string ToString() => Value.ToString();
}

public readonly record struct SagaInstanceId(Guid Value)
{
    public static SagaInstanceId New() => new(EntityIds.NewValue());

    public override string ToString() => Value.ToString();
}

public readonly record struct LedgerTransactionId(Guid Value)
{
    public static LedgerTransactionId New() => new(EntityIds.NewValue());

    public override string ToString() => Value.ToString();
}

public readonly record struct LedgerAccountId(Guid Value)
{
    public static LedgerAccountId New() => new(EntityIds.NewValue());

    public override string ToString() => Value.ToString();
}

public readonly record struct NudgeId(Guid Value)
{
    public static NudgeId New() => new(EntityIds.NewValue());

    public override string ToString() => Value.ToString();
}
