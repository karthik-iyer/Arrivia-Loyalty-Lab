using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Pricing;

public sealed class EligibilityStage : IPricingStage
{
    public PricingStageKind Kind => PricingStageKind.Eligibility;

    public int Order => 1;

    public PricingState Execute(PricingState state, PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);

        if (state.IsRejected)
        {
            return state;
        }

        var offer = request.Offer;
        var stay = request.Context.StayDate;

        if (!request.PermittedSuppliers.Contains(offer.SupplierId)
            || stay < offer.AvailableFrom
            || stay > offer.AvailableTo
            || request.Rules.OfType<EligibilityExclusionRule>().Any(rule => rule.AppliesTo(request.Context, request.AsOf)))
        {
            return state.Reject(Errors.OfferNotEligible);
        }

        return state;
    }
}

public sealed class BaseCostStage : IPricingStage
{
    public PricingStageKind Kind => PricingStageKind.BaseCost;

    public int Order => 2;

    public PricingState Execute(PricingState state, PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.IsRejected)
        {
            return state;
        }

        var netCost = request.Offer.NetRate + request.Offer.TaxesAndFees;
        return state with { RunningTotal = netCost, NetCost = netCost };
    }
}

public sealed class BaseMarkupStage : IPricingStage
{
    public PricingStageKind Kind => PricingStageKind.BaseMarkup;

    public int Order => 3;

    public PricingState Execute(PricingState state, PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.IsRejected)
        {
            return state;
        }

        return request.WinnerOf(PricingRuleKind.BaseMarkup) is BaseMarkupRule markup
            ? state with { RunningTotal = state.RunningTotal.ApplyPercent(markup.Markup) }
            : state;
    }
}

public sealed class TierAdjustmentStage : IPricingStage
{
    public PricingStageKind Kind => PricingStageKind.TierAdjustment;

    public int Order => 4;

    public PricingState Execute(PricingState state, PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.IsRejected)
        {
            return state;
        }

        return request.WinnerOf(PricingRuleKind.TierAdjustment) is TierAdjustmentRule tier
            ? state with { RunningTotal = state.RunningTotal.ApplyPercent(tier.Adjustment) }
            : state;
    }
}

public sealed class CampaignDiscountStage : IPricingStage
{
    public PricingStageKind Kind => PricingStageKind.CampaignDiscount;

    public int Order => 5;

    public PricingState Execute(PricingState state, PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.IsRejected)
        {
            return state;
        }

        return request.WinnerOf(PricingRuleKind.CampaignDiscount) is CampaignDiscountRule campaign
            ? state with { RunningTotal = state.RunningTotal.ApplyPercent(campaign.Adjustment) }
            : state;
    }
}

public sealed class MarginFloorStage : IPricingStage
{
    public PricingStageKind Kind => PricingStageKind.MarginFloor;

    public int Order => 6;

    public PricingState Execute(PricingState state, PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.IsRejected)
        {
            return state;
        }

        if (request.WinnerOf(PricingRuleKind.MarginFloor) is not MarginFloorRule floor)
        {
            return state;
        }

        var required = state.NetCost.ApplyPercent(floor.FloorAboveNet);
        return state.RunningTotal < required
            ? state with { RunningTotal = required }
            : state;
    }
}

public sealed class RoundingStage : IPricingStage
{
    public PricingStageKind Kind => PricingStageKind.Rounding;

    public int Order => 7;

    public PricingState Execute(PricingState state, PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.IsRejected)
        {
            return state;
        }

        return state with { RunningTotal = state.RunningTotal.RoundToCents() };
    }
}

public sealed class BurnCapStage : IPricingStage
{
    public PricingStageKind Kind => PricingStageKind.BurnCap;

    public int Order => 8;

    public PricingState Execute(PricingState state, PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.IsRejected)
        {
            return state;
        }

        if (request.WinnerOf(PricingRuleKind.BurnCap) is not BurnCapRule cap)
        {
            return state;
        }

        return state with { MaxCreditTender = state.RunningTotal.Multiply(cap.Cap.AsFraction()) };
    }
}

/// <summary>
/// Runs the eight stages in order and stops at the first rejection (FR-P-01).
/// </summary>
public sealed class PricingPipeline
{
    private readonly IPricingStage[] _stages;

    public PricingPipeline()
        : this(
        [
            new EligibilityStage(),
            new BaseCostStage(),
            new BaseMarkupStage(),
            new TierAdjustmentStage(),
            new CampaignDiscountStage(),
            new MarginFloorStage(),
            new RoundingStage(),
            new BurnCapStage(),
        ])
    {
    }

    public PricingPipeline(IEnumerable<IPricingStage> stages)
    {
        ArgumentNullException.ThrowIfNull(stages);
        _stages = [.. stages.OrderBy(stage => stage.Order)];
        if (_stages.Length != 8 || _stages.Select(s => s.Order).Distinct().Count() != 8)
        {
            throw new DomainException("The pricing pipeline must contain the eight stages in a total order.");
        }
    }

    public IReadOnlyList<IPricingStage> Stages => _stages;

    public PricingState Execute(PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var state = PricingState.Start(request.Offer.NetRate.Currency);
        foreach (var stage in _stages)
        {
            state = stage.Execute(state, request);
            if (state.IsRejected)
            {
                return state;
            }
        }

        return state;
    }
}
