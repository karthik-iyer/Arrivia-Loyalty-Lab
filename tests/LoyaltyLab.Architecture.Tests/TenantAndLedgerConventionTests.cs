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

public sealed class IdempotencyPortTests
{
    [Fact]
    public void IIdempotencyStore_is_find_or_insert_only()
    {
        var names = typeof(IIdempotencyStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name)
            .ToArray();

        names.Should().Contain("FindAsync");
        names.Should().Contain("SaveAsync");
        names.Should().OnlyContain(
            name => name.StartsWith("Find", StringComparison.Ordinal)
                || name.StartsWith("Save", StringComparison.Ordinal),
            "idempotency is insert-first plus lookup. An Update member would let a reused key overwrite the stored payload hash.");
    }
}
