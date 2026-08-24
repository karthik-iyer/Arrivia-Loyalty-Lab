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
        var eligible = request.PermittedSuppliers.Contains(offer.SupplierId)
            && stay >= offer.AvailableFrom
            && stay <= offer.AvailableTo
            && !request.Rules.OfType<EligibilityExclusionRule>().Any(rule => rule.AppliesTo(request.Context, request.AsOf));

        if (eligible)
        {
            return state.Record(this, "Offer is eligible.", state.RunningTotal);
        }

        return state
            .Record(this, "Offer is not eligible.", state.RunningTotal)
            .Reject(Errors.OfferNotEligible);
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
        return state.Record(
            this,
            $"Base cost (net {request.Offer.NetRate.Amount} + taxes {request.Offer.TaxesAndFees.Amount})",
            netCost,
            netCost: netCost);
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

        if (request.WinnerOf(PricingRuleKind.BaseMarkup) is not BaseMarkupRule markup)
        {
            return state.Record(this, "No base markup applied.", state.RunningTotal);
        }

        return state.Record(
            this,
            $"Base markup {PercentText.Signed(markup.Markup)}",
            state.RunningTotal.ApplyPercent(markup.Markup),
            markup.Id);
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

        if (request.WinnerOf(PricingRuleKind.TierAdjustment) is not TierAdjustmentRule tier)
        {
            return state.Record(this, "No tier adjustment.", state.RunningTotal);
        }

        return state.Record(
            this,
            $"Tier adjustment {PercentText.Signed(tier.Adjustment)}",
            state.RunningTotal.ApplyPercent(tier.Adjustment),
            tier.Id);
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

        if (request.WinnerOf(PricingRuleKind.CampaignDiscount) is not CampaignDiscountRule campaign)
        {
            return state.Record(this, "No campaign.", state.RunningTotal);
        }

        return state.Record(
            this,
            $"{campaign.CampaignCode} {PercentText.Signed(campaign.Adjustment)}",
            state.RunningTotal.ApplyPercent(campaign.Adjustment),
            campaign.Id);
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
            return state.Record(this, "No margin floor.", state.RunningTotal);
        }

        var required = state.NetCost.ApplyPercent(floor.FloorAboveNet);
        if (state.RunningTotal >= required)
        {
            return state.Record(
                this,
                $"Margin floor {PercentText.Signed(floor.FloorAboveNet)} satisfied",
                state.RunningTotal,
                floor.Id);
        }

        var raisedBy = required - state.RunningTotal;
        return state.Record(
            this,
            $"Margin floor {PercentText.Signed(floor.FloorAboveNet)}",
            required,
            floor.Id,
            wasClamped: true,
            clampReason: $"Raised by {raisedBy.Amount} to meet net cost {PercentText.Signed(floor.FloorAboveNet)} ({required.Amount}).");
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

        return state.Record(this, "Rounded to 2 decimal places.", state.RunningTotal.RoundToCents());
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
            return state.Record(this, "No burn cap.", state.RunningTotal);
        }

        var tender = state.RunningTotal.Multiply(cap.Cap.AsFraction());
        return state.Record(
            this,
            $"Burn cap {cap.Cap} → max credit tender {tender.Amount}",
            state.RunningTotal,
            cap.Id,
            maxCreditTender: tender);
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

file static class PercentText
{
    public static string Signed(Percent percent) =>
        percent.Value > 0m ? $"+{percent.Value}%" : $"{percent.Value}%";
}
