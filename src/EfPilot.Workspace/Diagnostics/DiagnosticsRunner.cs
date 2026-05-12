using EfPilot.Core.Diagnostics;
using EfPilot.Core.Workspace;
using EfPilot.Workspace.Discovery;

namespace EfPilot.Workspace.Diagnostics;

public sealed class DiagnosticsRunner(
    ProjectScanner projectScanner,
    DbContextScanner dbContextScanner,
    DesignTimeFactoryAnalyzer designTimeFactoryAnalyzer,
    StartupAnalyzer startupAnalyzer,
    MigrationAnalyzer migrationAnalyzer)
{
    public DiagnosticsReport Run(DiagnosticsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var projects = projectScanner
            .ScanProjects(request.SolutionDirectory)
            .Where(project => !IsTestProject(project))
            .ToList();

        var allDbContexts = dbContextScanner.ScanDbContexts(projects);
        var dbContexts = FilterDbContexts(allDbContexts, request.Profile);
        var designTimeFactories = designTimeFactoryAnalyzer.Analyze(projects);
        var startupProjects = startupAnalyzer.Analyze(projects);
        var migrationProjects = migrationAnalyzer.Analyze(projects);

        var findings = BuildFindings(
            dbContexts,
            designTimeFactories,
            startupProjects,
            migrationProjects,
            request.Verbose);

        return new DiagnosticsReport
        {
            SolutionDirectory = request.SolutionDirectory,
            Projects = projects,
            DbContexts = dbContexts,
            DesignTimeFactories = designTimeFactories,
            StartupProjects = startupProjects,
            MigrationProjects = migrationProjects,
            Findings = findings
        };
    }

    private static bool IsTestProject(WorkspaceProject project)
    {
        var nameParts = project.Name.Split(
            ['.', '-', '_'],
            StringSplitOptions.RemoveEmptyEntries);

        return nameParts.Any(part =>
            part.Equals("Test", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("Tests", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<DiscoveredDbContext> FilterDbContexts(
        IReadOnlyList<DiscoveredDbContext> dbContexts,
        Core.Configuration.EfPilotProfile? profile)
    {
        if (profile is null)
        {
            return dbContexts;
        }

        return dbContexts
            .Where(dbContext => string.Equals(
                dbContext.Name,
                profile.DbContext,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static IReadOnlyList<DiagnosticsFinding> BuildFindings(
        IReadOnlyList<DiscoveredDbContext> dbContexts,
        IReadOnlyList<DesignTimeFactoryInfo> designTimeFactories,
        IReadOnlyList<StartupProjectInfo> startupProjects,
        IReadOnlyList<MigrationProjectInfo> migrationProjects,
        bool verbose)
    {
        var findings = new List<DiagnosticsFinding>();

        if (dbContexts.Count == 0)
        {
            findings.Add(new DiagnosticsFinding
            {
                Category = "DbContexts",
                Severity = DiagnosticsSeverity.Error,
                Title = "No DbContexts detected",
                Message = "EfPilot could not find any classes inheriting from DbContext.",
                Suggestion = "Check that your DbContext classes are under a scanned .csproj and are not generated into bin/obj."
            });
        }

        foreach (var dbContext in dbContexts)
        {
            var factories = designTimeFactories
                .Where(factory => string.Equals(
                    factory.DbContextName,
                    dbContext.Name,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (factories.Count == 0)
            {
                findings.Add(new DiagnosticsFinding
                {
                    Category = "Design-time factory",
                    Severity = DiagnosticsSeverity.Warning,
                    Title = $"No design-time factory found for {dbContext.Name}",
                    Message = "Without IDesignTimeDbContextFactory<T>, EF Core often needs a startup project to construct the DbContext.",
                    Path = dbContext.Project.Path,
                    Suggestion = "Consider adding IDesignTimeDbContextFactory<T> for cleaner multi-project migration commands."
                });

                continue;
            }

            foreach (var factory in factories.Where(factory => factory.ReadsConfigurationFromCurrentDirectory))
            {
                findings.Add(new DiagnosticsFinding
                {
                    Category = "Design-time factory",
                    Severity = DiagnosticsSeverity.Warning,
                    Title = $"Design-time factory for {dbContext.Name} depends on the current directory",
                    Message = $"{factory.FactoryName} reads appsettings.json from Directory.GetCurrentDirectory().",
                    Path = factory.Path,
                    Suggestion = "Point the factory to a stable configuration path or use environment variables so EF can run without a startup project."
                });
            }

            if (verbose)
            {
                foreach (var factory in factories)
                {
                    if (factory.ReadsConfigurationFromCurrentDirectory)
                    {
                        continue;
                    }

                    findings.Add(new DiagnosticsFinding
                    {
                        Category = "Design-time factory",
                        Severity = DiagnosticsSeverity.Success,
                        Title = $"Design-time factory found for {dbContext.Name}",
                        Message = $"{factory.FactoryName} can create {dbContext.Name} at design time.",
                        Path = factory.Path
                    });
                }
            }
        }

        foreach (var startupProject in startupProjects.Where(project => project.CallsDatabaseMigrate))
        {
            findings.Add(new DiagnosticsFinding
            {
                Category = "Startup migration",
                Severity = DiagnosticsSeverity.Warning,
                Title = $"{startupProject.Project.Name} applies migrations at startup",
                Message = "Calling Database.Migrate() from application startup can make deployments harder to control.",
                Path = startupProject.ProgramPath,
                Suggestion = "Prefer idempotent SQL scripts or migration bundles as a separate CI/CD deployment step."
            });
        }

        if (dbContexts.Count > 0 && migrationProjects.All(project => project.ModelSnapshots.Count == 0))
        {
            findings.Add(new DiagnosticsFinding
            {
                Category = "Migrations",
                Severity = DiagnosticsSeverity.Warning,
                Title = "No ModelSnapshot files found",
                Message = "EfPilot detected DbContexts but no EF Core model snapshots.",
                Suggestion = "If this project uses EF migrations, verify that migrations are committed and stored in the expected project."
            });
        }

        foreach (var migrationProject in migrationProjects)
        {
            foreach (var emptyMigration in migrationProject.EmptyMigrations)
            {
                findings.Add(new DiagnosticsFinding
                {
                    Category = "Migrations",
                    Severity = DiagnosticsSeverity.Warning,
                    Title = "Empty migration detected",
                    Message = "This migration has an empty Up() method. It may exist only to update the model snapshot.",
                    Path = emptyMigration,
                    Suggestion = "Review whether this migration should be committed or whether snapshot drift should be handled explicitly."
                });
            }
        }

        if (dbContexts.Count > 0 &&
            migrationProjects.Count > 0 &&
            migrationProjects.All(project => !project.LooksDedicated))
        {
            findings.Add(new DiagnosticsFinding
            {
                Category = "Migrations",
                Severity = DiagnosticsSeverity.Info,
                Title = "No separate migrations project detected",
                Message = "Migrations appear to live inside application or infrastructure projects.",
                Suggestion = "For larger bounded contexts, consider a separate migrations project with IDesignTimeDbContextFactory<T>."
            });
        }

        if (findings.Count == 0)
        {
            findings.Add(new DiagnosticsFinding
            {
                Category = "Summary",
                Severity = DiagnosticsSeverity.Success,
                Title = "No migration workflow issues detected",
                Message = "EfPilot did not find obvious design-time, startup, snapshot, or empty migration issues."
            });
        }

        return findings;
    }
}
