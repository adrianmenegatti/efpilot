using EfPilot.Core.Diagnostics;
using EfPilot.Workspace.Diagnostics;
using EfPilot.Workspace.Discovery;

namespace EfPilot.Workspace.Tests.Diagnostics;

public sealed class DiagnosticsRunnerTests
{
    [Fact]
    public void Run_ShouldWarn_WhenDesignTimeFactoryReadsAppSettingsFromCurrentDirectory()
    {
        using var temp = TestDirectory.Create();
        WriteProjectFile(temp.Path, "Infrastructure/Infrastructure.csproj");
        WriteSolutionFile(temp.Path);

        File.WriteAllText(
            Path.Combine(temp.Path, "Infrastructure", "AppDbContext.cs"),
            """
            using Microsoft.EntityFrameworkCore;

            public sealed class AppDbContext : DbContext
            {
            }
            """);

        File.WriteAllText(
            Path.Combine(temp.Path, "Infrastructure", "AppDbContextFactory.cs"),
            """
            using Microsoft.EntityFrameworkCore.Design;
            using Microsoft.Extensions.Configuration;

            public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
            {
                public AppDbContext CreateDbContext(string[] args)
                {
                    var configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true)
                        .Build();

                    return null!;
                }
            }
            """);

        var runner = new DiagnosticsRunner(
            new ProjectScanner(),
            new DbContextScanner(),
            new DesignTimeFactoryAnalyzer(),
            new StartupAnalyzer(),
            new MigrationAnalyzer());

        var report = runner.Run(new DiagnosticsRequest
        {
            SolutionDirectory = temp.Path
        });

        Assert.Contains(report.Findings, finding =>
            finding.Severity == DiagnosticsSeverity.Warning &&
            finding.Title.Contains("depends on the current directory", StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteSolutionFile(string solutionDirectory)
    {
        File.WriteAllText(Path.Combine(solutionDirectory, "TestSolution.slnx"), "<Solution />");
    }

    private static void WriteProjectFile(string solutionDirectory, string relativePath)
    {
        var fullPath = Path.Combine(solutionDirectory, relativePath);
        var projectDirectory = Path.GetDirectoryName(fullPath)!;

        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(fullPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
    }
}
