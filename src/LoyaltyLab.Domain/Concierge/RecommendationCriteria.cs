using System.Collections.Frozen;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Concierge;

/// <summary>
/// Structured search intent. Optional fields left null mean unconstrained on that axis.
/// </summary>
public sealed record RecommendationCriteria(
    string? DestinationCode,
    IReadOnlySet<OfferTag> Tags,
    DateOnly? StayDate,
    Money? MaxBudget)
{
    public static RecommendationCriteria Unconstrained { get; } =
        new(null, FrozenSet<OfferTag>.Empty, StayDate: null, MaxBudget: null);

    public bool HasDestination => !string.IsNullOrWhiteSpace(DestinationCode);

    public bool HasTags => Tags.Count > 0;
}

public sealed record DestinationAlias(string Code, string DisplayName, IReadOnlyList<string> Aliases);

/// <summary>
/// Keyword vocabulary for destinations. Aliases are matched against request text (ADR-0009).
/// </summary>
public static class DestinationLexicon
{
    private static readonly Dictionary<string, string[]> KnownAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["MBJ"] = ["montego", "montego bay", "jamaica", "caribbean", "negril"],
            ["ZRH"] = ["zermatt", "alps", "switzerland", "alpine"],
            ["NYC"] = ["new york", "nyc", "manhattan", "brooklyn"],
        };

    public static IReadOnlyList<DestinationAlias> For(IEnumerable<Destination> destinations)
    {
        ArgumentNullException.ThrowIfNull(destinations);

        return destinations
            .GroupBy(destination => destination.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sample = group.First();
                var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    sample.Code,
                    sample.DisplayName,
                };

                if (KnownAliases.TryGetValue(sample.Code, out var extras))
                {
                    foreach (var extra in extras)
                    {
                        aliases.Add(extra);
                    }
                }

                return new DestinationAlias(
                    sample.Code,
                    sample.DisplayName,
                    aliases.OrderByDescending(alias => alias.Length).ToArray());
            })
            .ToArray();
    }
}

public sealed record ParsedCriteria(
    RecommendationCriteria Criteria,
    IReadOnlyList<string> InterpretedTerms);
