using EfPilot.Core.Workspace;
using EfPilot.Workspace.Diagnostics;

namespace EfPilot.Workspace.Tests.Diagnostics;

public sealed class StartupAnalyzerTests
{
    [Fact]
    public void Analyze_ShouldDetectDatabaseMigrateCalls()
    {
        using var temp = TestDirectory.Create();
        var project = CreateProject(temp.Path, "Api");

        File.WriteAllText(
            Path.Combine(project.Directory, "Program.cs"),
            """
            var builder = WebApplication.CreateBuilder(args);
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
            """);

        var analyzer = new StartupAnalyzer();

        var result = analyzer.Analyze([project]);

        Assert.Single(result);
        Assert.True(result[0].UsesWebApplicationBuilder);
        Assert.True(result[0].CallsDatabaseMigrate);
    }

    private static WorkspaceProject CreateProject(string root, string name)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);

        var projectPath = Path.Combine(directory, $"{name}.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");

        return new WorkspaceProject
        {
            Name = name,
            Path = projectPath,
            Directory = directory,
            Sdk = "Microsoft.NET.Sdk.Web"
        };
    }
}
