using EfPilot.Core.Workspace;
using EfPilot.Workspace.Discovery;

namespace EfPilot.Workspace.Tests.Discovery;

public sealed class DbContextScannerTests
{
    [Fact]
    public void ScanDbContexts_ShouldDetectSimpleDbContext()
    {
        using var temp = TestDirectory.Create();

        var project = CreateProject(temp.Path, "Infrastructure");

        File.WriteAllText(
            Path.Combine(project.Directory, "AppDbContext.cs"),
            """
            using Microsoft.EntityFrameworkCore;

            public sealed class AppDbContext : DbContext
            {
            }
            """);

        var scanner = new DbContextScanner();

        var result = scanner.ScanDbContexts([project]);

        Assert.Single(result);
        Assert.Equal("AppDbContext", result[0].Name);
    }

    [Fact]
    public void ScanDbContexts_ShouldDetectPrimaryConstructorDbContext()
    {
        using var temp = TestDirectory.Create();

        var project = CreateProject(temp.Path, "Infrastructure");

        File.WriteAllText(
            Path.Combine(project.Directory, "RateRiskDbContext.cs"),
            """
            using Microsoft.EntityFrameworkCore;

            public sealed class RateRiskDbContext(DbContextOptions<RateRiskDbContext> options)
                : DbContext(options)
            {
            }
            """);

        var scanner = new DbContextScanner();

        var result = scanner.ScanDbContexts([project]);

        Assert.Single(result);
        Assert.Equal("RateRiskDbContext", result[0].Name);
    }

    [Fact]
    public void ScanDbContexts_ShouldIgnoreDbContextFactories()
    {
        using var temp = TestDirectory.Create();

        var project = CreateProject(temp.Path, "Infrastructure");

        File.WriteAllText(
            Path.Combine(project.Directory, "RateRiskDbContextFactory.cs"),
            """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Design;

            public sealed class RateRiskDbContextFactory : IDesignTimeDbContextFactory<RateRiskDbContext>
            {
                public RateRiskDbContext CreateDbContext(string[] args) => null!;
            }
            """);

        var scanner = new DbContextScanner();

        var result = scanner.ScanDbContexts([project]);

        Assert.Empty(result);
    }

    private static WorkspaceProject CreateProject(string root, string name)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);

        var projectPath = Path.Combine(directory, $"{name}.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        return new WorkspaceProject
        {
            Name = name,
            Path = projectPath,
            Directory = directory,
            Sdk = "Microsoft.NET.Sdk"
        };
    }
}