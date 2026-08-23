using FluentAssertions;

namespace LoyaltyLab.Architecture.Tests;

/// <summary>
/// Conventions enforced by reading the source, because they are about how code is
/// written rather than what it references.
/// </summary>
public sealed class SourceConventionTests
{
    private static readonly string[] AmbientTimeAccess =
    [
        "DateTime.Now",
        "DateTime.UtcNow",
        "DateTime.Today",
        "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow",
    ];

    /// <summary>
    /// The adapter that implements <c>IClock</c> is the one place ambient time is legitimate.
    /// Everything else injects the clock so that effective dating, quote expiry, saga timeouts,
    /// and nudge cooldowns are all controllable in tests (NFR-12).
    /// </summary>
    private static readonly string[] ClockImplementations = ["SystemClock.cs"];

    [Fact]
    public void Production_code_reads_time_only_through_IClock()
    {
        var offenders = new List<string>();

        foreach (var file in Layers.ProductionSourceFiles())
        {
            if (ClockImplementations.Contains(file.Name, StringComparer.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file.FullName);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                offenders.AddRange(
                    AmbientTimeAccess
                        .Where(pattern => line.Contains(pattern, StringComparison.Ordinal))
                        .Select(pattern => $"{Relative(file)}:{i + 1} uses {pattern}"));
            }
        }

        offenders.Should().BeEmpty(
            "time must be injected via IClock, or a demo cannot be reproducible and an "
            + "expiry rule cannot be tested without waiting for it."
            + Environment.NewLine + string.Join(Environment.NewLine, offenders.Select(o => "  - " + o)));
    }

    private static string Relative(FileInfo file) =>
        Path.GetRelativePath(Layers.RepositoryRoot().FullName, file.FullName);
}
