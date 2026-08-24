using System.Globalization;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Pricing;

/// <summary>
/// One pipeline stage as observed. The trace is a return value, not a log (FR-P-07).
/// </summary>
public sealed record PriceTraceEntry(
    PricingStageKind Stage,
    int Order,
    string Description,
    PricingRuleId? AppliedRule,
    Money SubtotalBefore,
    Money SubtotalAfter,
    bool WasClamped,
    string? ClampReason);

/// <summary>
/// Role-aware view of a pricing run. Member and anonymous projections omit net cost and margin (FR-P-08).
/// </summary>
public sealed record PriceExplanation(
    IReadOnlyList<PriceTraceEntry> Stages,
    Money MemberPrice,
    Money? MaxCreditTender,
    Money? NetCost,
    Money? Margin)
{
    public static PriceExplanation From(PricingState state, AccessRole role)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (IsInternal(role))
        {
            Money? margin = state.IsRejected ? null : state.RunningTotal - state.NetCost;
            return new(
                state.Trace,
                state.RunningTotal,
                state.MaxCreditTender,
                state.IsRejected ? null : state.NetCost,
                margin);
        }

        var hiddenBase = state.NetCost;
        var stages = state.Trace
            .Where(entry => entry.Order >= (int)PricingStageKind.BaseMarkup)
            .Select(entry => Relativize(entry, hiddenBase))
            .ToArray();

        return new(stages, state.RunningTotal, state.MaxCreditTender, NetCost: null, Margin: null);
    }

    public bool RevealsNetRate => NetCost is not null || Margin is not null
        || Stages.Any(stage => stage.Stage is PricingStageKind.BaseCost or PricingStageKind.Eligibility);

    private static PriceTraceEntry Relativize(PriceTraceEntry entry, Money hiddenBase)
    {
        var shifted = entry with
        {
            SubtotalBefore = entry.SubtotalBefore - hiddenBase,
            SubtotalAfter = entry.SubtotalAfter - hiddenBase,
        };

        if (!shifted.WasClamped)
        {
            return shifted;
        }

        var raisedBy = entry.SubtotalAfter - entry.SubtotalBefore;
        return shifted with
        {
            ClampReason = $"Raised by {raisedBy.Amount.ToString(CultureInfo.InvariantCulture)} to meet the partner minimum.",
        };
    }

    private static bool IsInternal(AccessRole role) =>
        role is AccessRole.AccountManager or AccessRole.FinanceAnalyst or AccessRole.Operator;
}
