namespace LoyaltyLab.Architecture.Tests;

public sealed class PaymentSimIsolationTests
{
    [Fact]
    public void PaymentSim_references_no_platform_project()
    {
        var csproj = Path.Combine(
            Layers.RepositoryRoot().FullName,
            "src",
            "LoyaltyLab.PaymentSim",
            "LoyaltyLab.PaymentSim.csproj");

        File.ReadAllText(csproj).Should().NotContain(
            "<ProjectReference",
            "ADR-0006: a shared type would let the saga cheat about the far side.");
    }
}
