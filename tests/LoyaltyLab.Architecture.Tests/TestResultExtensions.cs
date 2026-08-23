using FluentAssertions;
using NetArchTest.Rules;

namespace LoyaltyLab.Architecture.Tests;

internal static class TestResultExtensions
{
    /// <summary>
    /// Asserts a NetArchTest result, naming the offending types. The default failure
    /// message reports only that a rule failed, which is the least useful half of the answer.
    /// </summary>
    public static void ShouldBeSuccessful(this TestResult result, string because)
    {
        var offenders = result.FailingTypeNames is null
            ? string.Empty
            : string.Join(Environment.NewLine, result.FailingTypeNames.Select(name => "  - " + name));

        result.IsSuccessful.Should().BeTrue(
            $"{because}{Environment.NewLine}Offending types:{Environment.NewLine}{offenders}");
    }
}
