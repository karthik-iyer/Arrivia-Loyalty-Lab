using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Pricing;

public enum PricingStageKind
{
    Eligibility = 1,
    BaseCost = 2,
    BaseMarkup = 3,
    TierAdjustment = 4,
    CampaignDiscount = 5,
    MarginFloor = 6,
    Rounding = 7,
    BurnCap = 8,
}

public sealed record PricingState(
    Money RunningTotal,
    Money NetCost,
    Money? MaxCreditTender,
    bool IsRejected,
    Error? RejectionReason,
    IReadOnlyList<PriceTraceEntry> Trace)
{
    public static PricingState Start(Currency currency) =>
        new(
            Money.Zero(currency),
            Money.Zero(currency),
            MaxCreditTender: null,
            IsRejected: false,
            RejectionReason: null,
            Trace: []);

    public PricingState Reject(Error reason) =>
        this with { IsRejected = true, RejectionReason = reason };

    public PricingState Record(
        IPricingStage stage,
        string description,
        Money subtotalAfter,
        PricingRuleId? appliedRule = null,
        bool wasClamped = false,
        string? clampReason = null,
        Money? netCost = null,
        Money? maxCreditTender = null)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(description);

        var entry = new PriceTraceEntry(
            stage.Kind,
            stage.Order,
            description,
            appliedRule,
            RunningTotal,
            subtotalAfter,
            wasClamped,
            clampReason);

        return this with
        {
            RunningTotal = subtotalAfter,
            NetCost = netCost ?? NetCost,
            MaxCreditTender = maxCreditTender ?? MaxCreditTender,
            Trace = [.. Trace, entry],
        };
    }
}

/// <summary>
/// One pricing run. Stages read this; they do not re-fetch partner or catalog (FR-X-03).
/// </summary>
public sealed record PricingRequest(
    PricingContext Context,
    TravelOffer Offer,
    IReadOnlySet<SupplierId> PermittedSuppliers,
    IReadOnlyList<PricingRule> Rules,
    DateTimeOffset AsOf)
{
    public PricingRule? WinnerOf(PricingRuleKind kind) =>
        PricingRulePrecedenceComparer.SelectWinner(
            Rules.Where(rule => rule.Kind == kind && rule.AppliesTo(Context, AsOf)));
}

public interface IPricingStage
{
    PricingStageKind Kind { get; }

    int Order { get; }

    PricingState Execute(PricingState state, PricingRequest request);
}
