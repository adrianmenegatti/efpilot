using EfPilot.Cli.Output;
using EfPilot.Cli.Profiles;
using EfPilot.Core.Configuration;
using EfPilot.Core.Diagnostics;
using EfPilot.Workspace.Configuration;
using EfPilot.Workspace.Diagnostics;
using EfPilot.Workspace.Discovery;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

public sealed class DiagnosticsCommand(
    SolutionFinder solutionFinder,
    EfPilotConfigStore configStore,
    ProfileResolver profileResolver,
    DiagnosticsRunner diagnosticsRunner) : EfPilotCommand
{
    public override async Task<int> ExecuteAsync(string[] args)
    {
        var profileName = CommandHelpers.GetOptionValue(args, "--profile");
        var verbose = CommandHelpers.HasFlag(args, "--verbose");
        var currentDirectory = Directory.GetCurrentDirectory();
        var solutionPath = solutionFinder.FindSolutionFile(currentDirectory);

        if (solutionPath is null)
        {
            ConsoleOutput.Error("No .sln or .slnx file found.");
            return 1;
        }

        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        var config = await configStore.LoadAsync(solutionDirectory);
        EfPilotProfile? profile = null;

        if (!string.IsNullOrWhiteSpace(profileName))
        {
            if (config is null)
            {
                ConsoleOutput.Error("A profile was specified, but efpilot is not initialized for this solution.");
                AnsiConsole.MarkupLine("Run [green]efpilot init[/] first or omit [green]--profile[/].");
                return 1;
            }

            profile = profileResolver.Resolve(config.Profiles, profileName);

            if (profile is null)
            {
                profileResolver.PrintProfileNotFound(config.Profiles);
                return 1;
            }
        }

        var report = diagnosticsRunner.Run(new DiagnosticsRequest
        {
            SolutionDirectory = solutionDirectory,
            Profile = profile,
            Verbose = verbose
        });

        PrintReport(report, solutionDirectory, profile, verbose);

        return report.HasErrors ? 1 : 0;
    }

    private static void PrintReport(
        DiagnosticsReport report,
        string solutionDirectory,
        EfPilotProfile? profile,
        bool verbose)
    {
        ConsoleOutput.CommandHeader("diagnostics");

        AnsiConsole.MarkupLine($"Solution: [blue]{Markup.Escape(Path.GetFileName(solutionDirectory))}[/]");

        if (profile is not null)
        {
            AnsiConsole.MarkupLine($"Profile: [green]{Markup.Escape(profile.Name)}[/]");
        }

        AnsiConsole.WriteLine();

        PrintSummary(report);
        PrintDbContexts(report, solutionDirectory);
        PrintDesignTimeFactories(report, solutionDirectory, verbose);
        PrintStartupProjects(report, solutionDirectory, verbose);
        PrintMigrationProjects(report, solutionDirectory, verbose);
        PrintFindings(report, solutionDirectory);
    }

    private static void PrintSummary(DiagnosticsReport report)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Area")
            .AddColumn("Count");

        table.AddRow("Projects", report.Projects.Count.ToString());
        table.AddRow("DbContexts", report.DbContexts.Count.ToString());
        table.AddRow("Design-time factories", report.DesignTimeFactories.Count.ToString());
        table.AddRow("Startup projects", report.StartupProjects.Count.ToString());
        table.AddRow("Migration projects", report.MigrationProjects.Count.ToString());

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void PrintDbContexts(
        DiagnosticsReport report,
        string solutionDirectory)
    {
        AnsiConsole.MarkupLine("[yellow]DbContexts[/]");

        if (report.DbContexts.Count == 0)
        {
            AnsiConsole.MarkupLine("  [grey](none detected)[/]");
            AnsiConsole.WriteLine();
            return;
        }

        foreach (var dbContext in report.DbContexts)
        {
            AnsiConsole.MarkupLine($"  [green]✔[/] {Markup.Escape(dbContext.Name)}");
            PrintPath(solutionDirectory, dbContext.Project.Path);
        }

        AnsiConsole.WriteLine();
    }

    private static void PrintDesignTimeFactories(
        DiagnosticsReport report,
        string solutionDirectory,
        bool verbose)
    {
        AnsiConsole.MarkupLine("[yellow]Design-time factories[/]");

        if (report.DesignTimeFactories.Count == 0)
        {
            AnsiConsole.MarkupLine("  [grey](none detected)[/]");
            AnsiConsole.WriteLine();
            return;
        }

        foreach (var factory in report.DesignTimeFactories)
        {
            AnsiConsole.MarkupLine(
                $"  [green]✔[/] {Markup.Escape(factory.FactoryName)} " +
                $"[grey]for {Markup.Escape(factory.DbContextName)}[/]");

            PrintPath(solutionDirectory, factory.Path);
        }

        AnsiConsole.WriteLine();
    }

    private static void PrintStartupProjects(
        DiagnosticsReport report,
        string solutionDirectory,
        bool verbose)
    {
        AnsiConsole.MarkupLine("[yellow]Startup projects[/]");

        if (report.StartupProjects.Count == 0)
        {
            AnsiConsole.MarkupLine("  [grey](none detected)[/]");
            AnsiConsole.WriteLine();
            return;
        }

        foreach (var startupProject in report.StartupProjects)
        {
            var marker = startupProject.CallsDatabaseMigrate ? "[yellow]⚠[/]" : "[green]✔[/]";
            var migrateInfo = startupProject.CallsDatabaseMigrate
                ? " [yellow]calls Database.Migrate()[/]"
                : string.Empty;

            AnsiConsole.MarkupLine(
                $"  {marker} {Markup.Escape(startupProject.Project.Name)}" + migrateInfo);

            if (verbose || startupProject.CallsDatabaseMigrate)
            {
                PrintPath(solutionDirectory, startupProject.ProgramPath);
            }
        }

        if (report.StartupProjects.All(project => !project.CallsDatabaseMigrate))
        {
            AnsiConsole.MarkupLine("  [green]✔[/] No Database.Migrate() calls detected.");
        }

        AnsiConsole.WriteLine();
    }

    private static void PrintMigrationProjects(
        DiagnosticsReport report,
        string solutionDirectory,
        bool verbose)
    {
        if (!verbose &&
            report.MigrationProjects.Count == 0 &&
            report.Findings.All(finding => finding.Category != "Migrations"))
        {
            return;
        }

        AnsiConsole.MarkupLine("[yellow]Migrations[/]");

        if (report.MigrationProjects.Count == 0)
        {
            AnsiConsole.MarkupLine("  [grey](none detected)[/]");
            AnsiConsole.WriteLine();
            return;
        }

        foreach (var migrationProject in report.MigrationProjects)
        {
            var separate = migrationProject.LooksDedicated ? " [grey]separate project[/]" : string.Empty;

            AnsiConsole.MarkupLine(
                $"  [green]✔[/] {Markup.Escape(migrationProject.Project.Name)}" + separate);

            if (verbose)
            {
                PrintPath(solutionDirectory, migrationProject.Project.Path);
            }

            AnsiConsole.MarkupLine(
                $"    Snapshots: [blue]{migrationProject.ModelSnapshots.Count}[/], " +
                $"empty migrations: [yellow]{migrationProject.EmptyMigrations.Count}[/]");

            if (!verbose)
            {
                continue;
            }

            foreach (var snapshot in migrationProject.ModelSnapshots)
            {
                AnsiConsole.MarkupLine("    Snapshot:");
                PrintPath(solutionDirectory, snapshot, indent: "      ");
            }

            foreach (var emptyMigration in migrationProject.EmptyMigrations)
            {
                AnsiConsole.MarkupLine("    Empty:");
                PrintPath(solutionDirectory, emptyMigration, indent: "      ");
            }
        }

        AnsiConsole.WriteLine();
    }

    private static void PrintFindings(
        DiagnosticsReport report,
        string solutionDirectory)
    {
        AnsiConsole.MarkupLine("[yellow]Findings[/]");

        foreach (var finding in report.Findings)
        {
            var color = GetSeverityColor(finding.Severity);
            var icon = GetSeverityIcon(finding.Severity);

            AnsiConsole.MarkupLine(
                $"  [{color}]{icon} {Markup.Escape(finding.Title)}[/]");

            AnsiConsole.MarkupLine(
                $"    {Markup.Escape(finding.Message)}");

            if (!string.IsNullOrWhiteSpace(finding.Path))
            {
                PrintPath(solutionDirectory, finding.Path, indent: "    ");
            }

            if (!string.IsNullOrWhiteSpace(finding.Suggestion))
            {
                AnsiConsole.MarkupLine(
                    $"    [blue]→[/] {Markup.Escape(finding.Suggestion)}");
            }
        }
    }

    private static string GetSeverityColor(DiagnosticsSeverity severity)
    {
        return severity switch
        {
            DiagnosticsSeverity.Success => "green",
            DiagnosticsSeverity.Info => "blue",
            DiagnosticsSeverity.Warning => "yellow",
            DiagnosticsSeverity.Error => "red",
            _ => "grey"
        };
    }

    private static string GetSeverityIcon(DiagnosticsSeverity severity)
    {
        return severity switch
        {
            DiagnosticsSeverity.Success => "✔",
            DiagnosticsSeverity.Info => "ℹ",
            DiagnosticsSeverity.Warning => "⚠",
            DiagnosticsSeverity.Error => "✖",
            _ => "-"
        };
    }

    private static string ToRelativePath(string baseDirectory, string path)
    {
        return Path.GetRelativePath(baseDirectory, path);
    }

    private static void PrintPath(
        string baseDirectory,
        string path,
        string indent = "    ")
    {
        AnsiConsole.MarkupLine(
            $"{indent}[grey]{Markup.Escape(ToRelativePath(baseDirectory, path))}[/]");
    }
}
