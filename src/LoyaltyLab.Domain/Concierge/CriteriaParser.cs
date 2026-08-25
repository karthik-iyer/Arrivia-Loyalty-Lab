using System.Globalization;
using System.Text.RegularExpressions;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Concierge;

/// <summary>
/// Rule-based keyword parser. Unrecognised text yields an unconstrained search, not an error (docs/04 §5.1).
/// </summary>
public static partial class CriteriaParser
{
    private static readonly (string Token, OfferTag Tag)[] TagTokens =
    [
        ("beaches", OfferTag.Beach),
        ("beach", OfferTag.Beach),
        ("ocean", OfferTag.Beach),
        ("skiing", OfferTag.Ski),
        ("ski", OfferTag.Ski),
        ("snow", OfferTag.Ski),
        ("alpine", OfferTag.Ski),
        ("downtown", OfferTag.City),
        ("urban", OfferTag.City),
        ("city", OfferTag.City),
        ("children", OfferTag.Family),
        ("family", OfferTag.Family),
        ("kids", OfferTag.Family),
        ("luxury", OfferTag.Luxury),
        ("spa", OfferTag.Luxury),
    ];

    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["january"] = 1,
        ["jan"] = 1,
        ["february"] = 2,
        ["feb"] = 2,
        ["march"] = 3,
        ["mar"] = 3,
        ["april"] = 4,
        ["apr"] = 4,
        ["may"] = 5,
        ["june"] = 6,
        ["jun"] = 6,
        ["july"] = 7,
        ["jul"] = 7,
        ["august"] = 8,
        ["aug"] = 8,
        ["september"] = 9,
        ["sept"] = 9,
        ["sep"] = 9,
        ["october"] = 10,
        ["oct"] = 10,
        ["november"] = 11,
        ["nov"] = 11,
        ["december"] = 12,
        ["dec"] = 12,
    };

    public static ParsedCriteria Parse(
        string? text,
        IReadOnlyList<DestinationAlias> destinations,
        DateOnly calendarAnchor,
        RecommendationCriteria? overlay = null)
    {
        ArgumentNullException.ThrowIfNull(destinations);

        var haystack = " " + Normalize(text) + " ";
        var terms = new List<string>();
        string? destination = null;
        var tags = new HashSet<OfferTag>();
        DateOnly? stay = null;
        Money? budget = null;

        foreach (var alias in destinations.SelectMany(PairAliases).OrderByDescending(pair => pair.Phrase.Length))
        {
            if (ContainsPhrase(haystack, alias.Phrase))
            {
                destination = alias.Code;
                terms.Add(alias.Label);
                break;
            }
        }

        foreach (var month in Months.Keys.OrderByDescending(key => key.Length))
        {
            if (!ContainsPhrase(haystack, month))
            {
                continue;
            }

            stay = new DateOnly(calendarAnchor.Year, Months[month], 15);
            terms.Add(ToTitle(month));
            break;
        }

        foreach (var (token, tag) in TagTokens)
        {
            if (ContainsPhrase(haystack, token) && tags.Add(tag))
            {
                terms.Add(token);
            }
        }

        if (TryReadBudget(haystack, out var amount))
        {
            budget = Money.Of(amount, Currency.Usd);
            terms.Add("$" + amount.ToString("0.##", CultureInfo.InvariantCulture));
        }

        var parsed = new RecommendationCriteria(destination, tags, stay, budget);
        return new ParsedCriteria(ApplyOverlay(parsed, overlay), Distinct(terms));
    }

    private static RecommendationCriteria ApplyOverlay(
        RecommendationCriteria parsed,
        RecommendationCriteria? overlay)
    {
        if (overlay is null)
        {
            return parsed;
        }

        return new RecommendationCriteria(
            overlay.DestinationCode ?? parsed.DestinationCode,
            overlay.HasTags ? overlay.Tags : parsed.Tags,
            overlay.StayDate ?? parsed.StayDate,
            overlay.MaxBudget ?? parsed.MaxBudget);
    }

    private static IEnumerable<(string Phrase, string Code, string Label)> PairAliases(DestinationAlias destination)
    {
        yield return (Normalize(destination.DisplayName), destination.Code, destination.DisplayName);
        foreach (var alias in destination.Aliases)
        {
            yield return (Normalize(alias), destination.Code, destination.DisplayName);
        }
    }

    private static bool TryReadBudget(string haystack, out decimal amount)
    {
        var match = BudgetPattern().Match(haystack);
        if (match.Success
            && decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
            && amount > 0m)
        {
            return true;
        }

        amount = 0m;
        return false;
    }

    private static bool ContainsPhrase(string haystack, string phrase)
    {
        var needle = " " + Normalize(phrase) + " ";
        return needle.Length > 2 && haystack.Contains(needle, StringComparison.Ordinal);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Trim().Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ToTitle(string token) =>
        string.IsNullOrEmpty(token) ? token : char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant();

    private static List<string> Distinct(IEnumerable<string> terms)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        foreach (var term in terms)
        {
            if (seen.Add(term))
            {
                ordered.Add(term);
            }
        }

        return ordered;
    }

    [GeneratedRegex(@"(?:\$|under|budget|max)\s*\$?\s*(\d+(?:\.\d{1,2})?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BudgetPattern();
}
