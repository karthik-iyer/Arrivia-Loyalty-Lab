using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Tenancy;

/// <summary>
/// Presentation tokens for a partner brand. A new brand is configuration, not a code change (FR-X-04).
/// </summary>
public sealed record PartnerTheme
{
    public PartnerTheme(string primaryColor, string surfaceColor, string accentColor, string? logoUrl = null)
    {
        PrimaryColor = RequireCssColor(primaryColor, nameof(primaryColor));
        SurfaceColor = RequireCssColor(surfaceColor, nameof(surfaceColor));
        AccentColor = RequireCssColor(accentColor, nameof(accentColor));
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
    }

    public string PrimaryColor { get; }

    public string SurfaceColor { get; }

    public string AccentColor { get; }

    public string? LogoUrl { get; }

    private static string RequireCssColor(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length != 7 || trimmed[0] != '#')
        {
            throw new DomainException($"{name} must be a #RRGGBB colour.");
        }

        for (var i = 1; i < trimmed.Length; i++)
        {
            if (!char.IsAsciiHexDigit(trimmed[i]))
            {
                throw new DomainException($"{name} must be a #RRGGBB colour.");
            }
        }

        return trimmed.ToUpperInvariant();
    }
}
