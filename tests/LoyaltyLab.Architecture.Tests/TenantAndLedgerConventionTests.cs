using System.Reflection;
using LoyaltyLab.Application.Abstractions;

namespace LoyaltyLab.Architecture.Tests;

public sealed class LedgerPortTests
{
    [Fact]
    public void ILedgerRepository_exposes_no_update_or_delete()
    {
        var names = typeof(ILedgerRepository)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name)
            .ToArray();

        names.Should().Contain("AddAsync");
        names.Should().OnlyContain(
            name => name.StartsWith("Add", StringComparison.Ordinal)
                || name.StartsWith("Get", StringComparison.Ordinal)
                || name.StartsWith("Find", StringComparison.Ordinal)
                || name.StartsWith("List", StringComparison.Ordinal),
            "the ledger port is append + read. An Update/Delete/Remove member would make FR-L-01 a comment instead of a type.");
    }
}
