using NetArchTest.Rules;

namespace LoyaltyLab.Architecture.Tests;

public sealed class McpAdapterTests
{
    [Fact]
    public void Mcp_types_do_not_reference_Domain()
    {
        var result = NetArchTest.Rules.Types.InAssembly(Layers.ApiAssembly)
            .That()
            .ResideInNamespace("LoyaltyLab.Api.Mcp")
            .Should()
            .NotHaveDependencyOn(Layers.Domain)
            .GetResult();

        result.ShouldBeSuccessful(
            "MCP adapters must not reference Domain. Tenant rules and pricing live in use cases; "
            + "Api/Mcp is a forwarding layer (docs/04 §5.4, ADR-0010).");
    }

    [Fact]
    public void Mcp_types_do_not_reference_Application()
    {
        var result = NetArchTest.Rules.Types.InAssembly(Layers.ApiAssembly)
            .That()
            .ResideInNamespace("LoyaltyLab.Api.Mcp")
            .Should()
            .NotHaveDependencyOn(Layers.Application)
            .GetResult();

        result.ShouldBeSuccessful(
            "MCP adapters call IMcpUseCases, not use cases directly, so they cannot grow a second copy of a rule.");
    }

    [Fact]
    public void Mcp_source_contains_no_conditional_business_logic()
    {
        var folder = Path.Combine(Layers.RepositoryRoot().FullName, "src", "LoyaltyLab.Api", "Mcp");
        Directory.Exists(folder).Should().BeTrue("the MCP adapters live under src/LoyaltyLab.Api/Mcp");

        var offenders = new List<string>();
        foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file.FullName);
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*'))
                {
                    continue;
                }

                if (ContainsConditional(trimmed))
                {
                    offenders.Add($"{Relative(file)}:{i + 1}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "MCP tool classes must forward to use cases without branching. Offenders:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders.Select(item => "  - " + item)));
    }

    private static bool ContainsConditional(string line) =>
        line.Contains(" if ", StringComparison.Ordinal)
        || line.StartsWith("if ", StringComparison.Ordinal)
        || line.StartsWith("else", StringComparison.Ordinal)
        || line.StartsWith("switch ", StringComparison.Ordinal)
        || line.Contains(" switch ", StringComparison.Ordinal);

    private static string Relative(FileInfo file) =>
        Path.GetRelativePath(Layers.RepositoryRoot().FullName, file.FullName);
}
