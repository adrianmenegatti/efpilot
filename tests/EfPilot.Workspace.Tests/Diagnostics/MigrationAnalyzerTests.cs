using EfPilot.Core.Workspace;
using EfPilot.Workspace.Diagnostics;

namespace EfPilot.Workspace.Tests.Diagnostics;

public sealed class MigrationAnalyzerTests
{
    [Fact]
    public void Analyze_ShouldDetectSnapshotsAndEmptyMigrations()
    {
        using var temp = TestDirectory.Create();
        var project = CreateProject(temp.Path, "Billing.Migrations");
        var migrationsDirectory = Path.Combine(project.Directory, "Migrations");
        Directory.CreateDirectory(migrationsDirectory);

        File.WriteAllText(
            Path.Combine(migrationsDirectory, "BillingDbContextModelSnapshot.cs"),
            "public sealed class BillingDbContextModelSnapshot { }");

        File.WriteAllText(
            Path.Combine(migrationsDirectory, "20260512000100_Empty.cs"),
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            public partial class Empty : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                }
            }
            """);

        var analyzer = new MigrationAnalyzer();

        var result = analyzer.Analyze([project]);

        Assert.Single(result);
        Assert.True(result[0].LooksDedicated);
        Assert.Single(result[0].ModelSnapshots);
        Assert.Single(result[0].EmptyMigrations);
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
