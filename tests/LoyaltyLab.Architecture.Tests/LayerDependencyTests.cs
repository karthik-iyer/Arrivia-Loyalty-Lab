using FluentAssertions;
using NetArchTest.Rules;

namespace LoyaltyLab.Architecture.Tests;

/// <summary>
/// The dependency rule from ADR-0001: dependencies point inward, never outward.
/// These exist so the claim cannot quietly become false.
/// </summary>
public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_depends_on_no_other_layer()
    {
        var result = Types.InAssembly(Layers.DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(Layers.Application, Layers.Infrastructure, Layers.Api)
            .GetResult();

        result.ShouldBeSuccessful(
            "Domain is the innermost layer. If it needs something from an outer layer, "
            + "the dependency should be inverted with a port defined in Application.");
    }

    [Fact]
    public void Domain_depends_on_no_framework()
    {
        var result = Types.InAssembly(Layers.DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions.DependencyInjection",
                "System.Data",
                "System.Net.Http",
                "System.Text.Json")
            .GetResult();

        result.ShouldBeSuccessful(
            "Domain must stay testable with no database, no HTTP, and no serializer. "
            + "Persistence and transport concerns belong in Infrastructure.");
    }

    [Fact]
    public void Application_does_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(Layers.ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(Layers.Infrastructure, Layers.Api)
            .GetResult();

        result.ShouldBeSuccessful(
            "Application defines ports; Infrastructure implements them. A reference in this "
            + "direction means an adapter leaked into a use case.");
    }

    [Fact]
    public void Application_does_not_depend_on_persistence_or_transport()
    {
        var result = Types.InAssembly(Layers.ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "System.Net.Http")
            .GetResult();

        result.ShouldBeSuccessful(
            "Use cases must be exercisable with fake ports and no host. An EF Core or "
            + "HttpClient reference here defeats that.");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        var result = Types.InAssembly(Layers.InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOn(Layers.Api)
            .GetResult();

        result.ShouldBeSuccessful(
            "Adapters must not reach back into the composition root.");
    }
}
