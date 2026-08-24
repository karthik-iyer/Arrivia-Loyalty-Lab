using System.Diagnostics.CodeAnalysis;

namespace LoyaltyLab.Domain.Common;

/// <summary>
/// A named business failure. <see cref="Code"/> is stable and maps to the error catalog.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Error is the ubiquitous language and the catalog type in docs/04. VB's Error keyword is not a consumer of this codebase.")]
public sealed record Error(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?>? Details = null)
{
    public static Error Of(string code, string message) => new(code, message);
}
