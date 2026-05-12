using EfPilot.Core.Diagnostics;
using EfPilot.Core.Workspace;

namespace EfPilot.Workspace.Diagnostics;

public sealed class StartupAnalyzer
{
    public IReadOnlyList<StartupProjectInfo> Analyze(
        IReadOnlyList<WorkspaceProject> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        return projects
            .Select(project => new
            {
                Project = project,
                ProgramPath = Path.Combine(project.Directory, "Program.cs")
            })
            .Where(candidate => File.Exists(candidate.ProgramPath))
            .Select(candidate =>
            {
                var content = File.ReadAllText(candidate.ProgramPath);

                return new StartupProjectInfo
                {
                    Project = candidate.Project,
                    ProgramPath = candidate.ProgramPath,
                    UsesWebApplicationBuilder = content.Contains(
                        "WebApplication.CreateBuilder",
                        StringComparison.Ordinal),
                    UsesGenericHostBuilder = content.Contains(
                        "Host.CreateDefaultBuilder",
                        StringComparison.Ordinal),
                    CallsDatabaseMigrate =
                        content.Contains(".Database.Migrate(", StringComparison.Ordinal) ||
                        content.Contains("Database.MigrateAsync(", StringComparison.Ordinal)
                };
            })
            .OrderBy(startup => startup.Project.Name)
            .ToList();
    }
}
