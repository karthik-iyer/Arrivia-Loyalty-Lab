using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Concierge;

namespace LoyaltyLab.Application.Concierge;

public sealed record NarrationOutcome(string Narrative, RecommendationAudit Audit);

/// <summary>
/// Accepts narrator prose only when it is grounded in the facts; otherwise the template stands (ADR-0009).
/// </summary>
public static class GroundedNarration
{
    public static async Task<NarrationOutcome> ApplyAsync(
        IOfferNarrator narrator,
        RecommendationSet facts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrator);
        ArgumentNullException.ThrowIfNull(facts);

        var template = NarrationTemplate.Render(facts);
        var spoken = await narrator.NarrateAsync(facts, cancellationToken);
        var applied = spoken.IsSuccess
            && NarrationValidator.IsGrounded(spoken.Value, facts)
            && !string.Equals(spoken.Value, template, StringComparison.Ordinal);

        return applied
            ? new NarrationOutcome(spoken.Value, facts.Audit with { NarrationApplied = true })
            : new NarrationOutcome(template, facts.Audit with { NarrationApplied = false });
    }
}
