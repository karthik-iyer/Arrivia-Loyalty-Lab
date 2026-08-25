using System.Globalization;
using System.Text.RegularExpressions;

namespace LoyaltyLab.Domain.Concierge;

/// <summary>
/// Deterministic prose with no model and no network (FR-C-07, NFR-08).
/// </summary>
public static class NarrationTemplate
{
    public static string Render(RecommendationSet facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return facts.Recommendations.Count switch
        {
            0 => "No stays fit those dates and credits.",
            1 => $"{facts.Recommendations[0].PropertyName} fits your dates, and credits cover most of it.",
            var count => $"{count.ToString(CultureInfo.InvariantCulture)} stays fit your dates, and credits cover most of the first.",
        };
    }
}
