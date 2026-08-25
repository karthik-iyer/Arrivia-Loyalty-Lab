using System.Globalization;
using System.Text.RegularExpressions;

namespace LoyaltyLab.Domain.Concierge;

/// <summary>
/// Rejects prose that introduces a currency amount or property name not present in the facts (FR-C-06).
/// </summary>
public static partial class NarrationValidator
{
    public static bool IsGrounded(string narration, RecommendationSet facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (string.IsNullOrWhiteSpace(narration))
        {
            return false;
        }

        var allowedAmounts = facts.Recommendations
            .Select(item => item.MemberPrice.Amount)
            .ToHashSet();
        foreach (Match match in AmountPattern().Matches(narration))
        {
            var raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                || !allowedAmounts.Contains(amount))
            {
                return false;
            }
        }

        var allowedNames = facts.Recommendations
            .Select(item => Normalize(item.PropertyName))
            .ToHashSet(StringComparer.Ordinal);
        foreach (Match match in PropertyPattern().Matches(narration))
        {
            if (!allowedNames.Contains(Normalize(match.Value)))
            {
                return false;
            }
        }

        return true;
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    [GeneratedRegex(@"\$\s*(\d+(?:\.\d{1,2})?)|(?<![\d.])(\d+\.\d{2})(?![\d])", RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();

    [GeneratedRegex(
        """\b[A-Z][A-Za-z]+(?:\s+[A-Z][A-Za-z]+)*\s+(?:Resort|Lodge|Inn|Hotel|Villas|Suites|House|Chalet|Cabin|Residences|Atelier)\b""",
        RegexOptions.CultureInvariant)]
    private static partial Regex PropertyPattern();
}
