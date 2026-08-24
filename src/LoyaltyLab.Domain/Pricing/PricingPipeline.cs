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
    Error? RejectionReason)
{
    public static PricingState Start(Currency currency) =>
        new(Money.Zero(currency), Money.Zero(currency), MaxCreditTender: null, IsRejected: false, RejectionReason: null);

    public PricingState Reject(Error reason) =>
        this with { IsRejected = true, RejectionReason = reason };
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
