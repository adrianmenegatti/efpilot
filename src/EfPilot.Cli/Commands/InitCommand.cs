using EfPilot.Core.Configuration;
using EfPilot.Core.Profiles;
using EfPilot.Core.Workspace;
using EfPilot.Workspace.Configuration;
using EfPilot.Workspace.Discovery;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

public sealed class InitCommand : EfPilotCommand
{
    public override async Task<int> ExecuteAsync(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        var solutionFinder = new SolutionFinder();
        var solutionPath = solutionFinder.FindSolutionFile(currentDirectory);

        if (solutionPath is null)
        {
            AnsiConsole.MarkupLine("[red]No .sln or .slnx file found.[/]");
            return 1;
        }

        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        var solutionFileName = Path.GetFileName(solutionPath);

        AnsiConsole.MarkupLine($"Found solution: [green]{solutionFileName}[/]");

        var projectScanner = new ProjectScanner();
        var projects = projectScanner.ScanProjects(solutionDirectory);

        AnsiConsole.MarkupLine($"Found projects: [green]{projects.Count}[/]");

        var dbContextScanner = new DbContextScanner();
        var dbContexts = dbContextScanner.ScanDbContexts(projects);

        PrintDetectedDbContexts(dbContexts, projects);

        var profiles = BuildProfiles(solutionDirectory, dbContexts, projects);

        if (profiles.Count == 0)
        {
            profiles.Add(CreateManualProfile());
        }

        var config = new EfPilotConfig
        {
            Solution = solutionFileName,
            Profiles = profiles
        };

        var store = new EfPilotConfigStore();
        await store.SaveAsync(solutionDirectory, config);

        var configPath = store.GetConfigPath(solutionDirectory);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]efpilot initialized successfully.[/]");
        AnsiConsole.MarkupLine($"Config saved to: [blue]{configPath}[/]");

        return 0;
    }

    private static void PrintDetectedDbContexts(
        IReadOnlyList<DiscoveredDbContext> dbContexts,
        IReadOnlyList<WorkspaceProject> projects)
    {
        if (dbContexts.Count == 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]No DbContext classes detected. Falling back to manual setup.[/]");
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Detected DbContexts:[/]");

        var startupDetector = new StartupProjectDetector();

        foreach (var dbContext in dbContexts)
        {
            AnsiConsole.MarkupLine($"- [green]{dbContext.Name}[/]");
            AnsiConsole.MarkupLine($"  Project: [blue]{dbContext.Project.Path}[/]");

            var candidates = startupDetector
                .DetectCandidates(dbContext, projects)
                .Take(3)
                .ToList();

            foreach (var candidate in candidates)
            {
                AnsiConsole.MarkupLine(
                    $"  Startup candidate: [yellow]{candidate.Project.Name}[/] " +
                    $"(score: {candidate.Score})");

                AnsiConsole.MarkupLine(
                    $"    Reasons: {string.Join(", ", candidate.Reasons)}");
            }

            AnsiConsole.WriteLine();
        }
    }

    private static List<EfPilotProfile> BuildProfiles(
        string solutionDirectory,
        IReadOnlyList<DiscoveredDbContext> dbContexts,
        IReadOnlyList<WorkspaceProject> projects)
    {
        var profiles = new List<EfPilotProfile>();

        if (dbContexts.Count == 0)
        {
            return profiles;
        }

        var createDetectedProfiles = AnsiConsole.Confirm(
            "Create profiles from detected DbContexts?",
            defaultValue: true);

        if (!createDetectedProfiles)
        {
            return profiles;
        }

        var startupDetector = new StartupProjectDetector();

        foreach (var dbContext in dbContexts)
        {
            var candidates = startupDetector
                .DetectCandidates(dbContext, projects)
                .ToList();

            if (candidates.Count == 0)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]No startup project candidates found for {dbContext.Name}. Skipping.[/]");
                continue;
            }

            var selectedCandidate = candidates.Count == 1
                ? candidates[0]
                : AnsiConsole.Prompt(
                    new SelectionPrompt<StartupProjectCandidate>()
                        .Title($"Select startup project for [green]{dbContext.Name}[/]:")
                        .PageSize(10)
                        .UseConverter(candidate =>
                            $"{candidate.Project.Name} (score: {candidate.Score})")
                        .AddChoices(candidates));

            var profileNameSuggestion = ProfileNameGenerator.FromDbContext(dbContext.Name);

            var profileName = AnsiConsole.Ask(
                $"Profile name for [green]{dbContext.Name}[/]:",
                profileNameSuggestion);

            var migrationsFolder = AnsiConsole.Ask<string?>(
                $"Migrations folder for [green]{dbContext.Name}[/] (optional):",
                null);

            profiles.Add(new EfPilotProfile
            {
                Name = profileName,
                DbContext = dbContext.Name,
                Project = CommandHelpers.ToRelativePath(solutionDirectory, dbContext.Project.Path),
                StartupProject = CommandHelpers.ToRelativePath(solutionDirectory, selectedCandidate.Project.Path),
                MigrationsFolder = string.IsNullOrWhiteSpace(migrationsFolder)
                    ? null
                    : migrationsFolder
            });
        }

        return profiles;
    }

    private static EfPilotProfile CreateManualProfile()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Manual configuration[/]");

        var profileName = AnsiConsole.Ask<string>("Profile name:");
        var dbContextName = AnsiConsole.Ask<string>("DbContext name:");
        var project = AnsiConsole.Ask<string>("Project path:");
        var startupProject = AnsiConsole.Ask<string>("Startup project path:");
        var migrationsFolder = AnsiConsole.Ask<string?>("Migrations folder (optional):");

        return new EfPilotProfile
        {
            Name = profileName,
            DbContext = dbContextName,
            Project = project,
            StartupProject = startupProject,
            MigrationsFolder = string.IsNullOrWhiteSpace(migrationsFolder)
                ? null
                : migrationsFolder
        };
    }
}