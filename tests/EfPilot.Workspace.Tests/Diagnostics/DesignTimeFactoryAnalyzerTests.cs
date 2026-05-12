using EfPilot.Core.Workspace;
using EfPilot.Workspace.Diagnostics;

namespace EfPilot.Workspace.Tests.Diagnostics;

public sealed class DesignTimeFactoryAnalyzerTests
{
    [Fact]
    public void Analyze_ShouldDetectDesignTimeFactory()
    {
        using var temp = TestDirectory.Create();
        var project = CreateProject(temp.Path, "Infrastructure");

        File.WriteAllText(
            Path.Combine(project.Directory, "RateRiskDbContextFactory.cs"),
            """
            using Microsoft.EntityFrameworkCore.Design;

            public sealed class RateRiskDbContextFactory : IDesignTimeDbContextFactory<RateRiskDbContext>
            {
            }
            """);

        var analyzer = new DesignTimeFactoryAnalyzer();

        var result = analyzer.Analyze([project]);

        Assert.Single(result);
        Assert.Equal("RateRiskDbContextFactory", result[0].FactoryName);
        Assert.Equal("RateRiskDbContext", result[0].DbContextName);
        Assert.False(result[0].ReadsConfigurationFromCurrentDirectory);
    }

    [Fact]
    public void Analyze_ShouldDetectFactoriesThatReadAppSettingsFromCurrentDirectory()
    {
        using var temp = TestDirectory.Create();
        var project = CreateProject(temp.Path, "Infrastructure");

        File.WriteAllText(
            Path.Combine(project.Directory, "RateRiskDbContextFactory.cs"),
            """
            using Microsoft.EntityFrameworkCore.Design;
            using Microsoft.Extensions.Configuration;

            public sealed class RateRiskDbContextFactory : IDesignTimeDbContextFactory<RateRiskDbContext>
            {
                public RateRiskDbContext CreateDbContext(string[] args)
                {
                    var configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true)
                        .Build();

                    return null!;
                }
            }
            """);

        var analyzer = new DesignTimeFactoryAnalyzer();

        var result = analyzer.Analyze([project]);

        Assert.Single(result);
        Assert.True(result[0].ReadsConfigurationFromCurrentDirectory);
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
