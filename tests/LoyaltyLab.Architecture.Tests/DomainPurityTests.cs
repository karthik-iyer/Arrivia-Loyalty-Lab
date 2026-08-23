using System.Reflection;
using FluentAssertions;

namespace LoyaltyLab.Architecture.Tests;

/// <summary>
/// Rules the Domain layer must obey that a dependency graph cannot express.
/// </summary>
public sealed class DomainPurityTests
{
    private static readonly Type[] BannedNumericTypes = [typeof(double), typeof(float)];

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [Fact]
    public void Domain_uses_no_binary_floating_point_types()
    {
        var offenders = new List<string>();

        foreach (var type in DeclaredTypes(Layers.DomainAssembly))
        {
            foreach (var field in type.GetFields(AllMembers).Where(f => !IsCompilerGenerated(f)))
            {
                if (BannedNumericTypes.Contains(Unwrap(field.FieldType)))
                {
                    offenders.Add($"{type.FullName}.{field.Name} : {field.FieldType.Name}");
                }
            }

            foreach (var property in type.GetProperties(AllMembers))
            {
                if (BannedNumericTypes.Contains(Unwrap(property.PropertyType)))
                {
                    offenders.Add($"{type.FullName}.{property.Name} : {property.PropertyType.Name}");
                }
            }

            foreach (var method in type.GetMethods(AllMembers).Where(m => m.DeclaringType == type))
            {
                if (BannedNumericTypes.Contains(Unwrap(method.ReturnType)))
                {
                    offenders.Add($"{type.FullName}.{method.Name}() returns {method.ReturnType.Name}");
                }

                offenders.AddRange(
                    method.GetParameters()
                        .Where(p => BannedNumericTypes.Contains(Unwrap(p.ParameterType)))
                        .Select(p => $"{type.FullName}.{method.Name}({p.Name} : {p.ParameterType.Name})"));
            }
        }

        offenders.Should().BeEmpty(
            "money and percentages must use decimal (FR-X-06). Binary floating point cannot "
            + "represent 0.1 exactly, so a pricing pipeline built on it drifts by cents."
            + Environment.NewLine + string.Join(Environment.NewLine, offenders.Select(o => "  - " + o)));
    }

    private static IEnumerable<Type> DeclaredTypes(Assembly assembly) =>
        assembly.GetTypes().Where(t => !IsCompilerGenerated(t));

    private static Type Unwrap(Type type) =>
        Nullable.GetUnderlyingType(type) ?? (type.IsArray ? type.GetElementType()! : type);

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.Name.Contains('<', StringComparison.Ordinal)
        || member.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);
}
