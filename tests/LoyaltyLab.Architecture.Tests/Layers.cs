using System.Reflection;
using LoyaltyLab.Api;
using LoyaltyLab.Application;
using LoyaltyLab.Domain;
using LoyaltyLab.Infrastructure;

namespace LoyaltyLab.Architecture.Tests;

/// <summary>
/// Assembly handles and namespace names shared by every architecture test.
/// </summary>
internal static class Layers
{
    public const string Domain = "LoyaltyLab.Domain";
    public const string Application = "LoyaltyLab.Application";
    public const string Infrastructure = "LoyaltyLab.Infrastructure";
    public const string Api = "LoyaltyLab.Api";

    public static Assembly DomainAssembly => typeof(DomainAssembly).Assembly;
    public static Assembly ApplicationAssembly => typeof(ApplicationAssembly).Assembly;
    public static Assembly InfrastructureAssembly => typeof(InfrastructureAssembly).Assembly;
    public static Assembly ApiAssembly => typeof(ApiAssembly).Assembly;

    /// <summary>
    /// Walks up from the test output directory to the directory holding Directory.Build.props.
    /// Source-scanning tests need the repository on disk, not the compiled output.
    /// </summary>
    public static DirectoryInfo RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
        {
            directory = directory.Parent;
        }

        return directory
            ?? throw new InvalidOperationException(
                "Could not locate the repository root: no Directory.Build.props found above " + AppContext.BaseDirectory);
    }

    public static IEnumerable<FileInfo> ProductionSourceFiles() =>
        RepositoryRoot()
            .GetDirectories("src").Single()
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
