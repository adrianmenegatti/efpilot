using EfPilot.Core.Configuration;
using EfPilot.Core.Profiles;
using EfPilot.Core.Workspace;
using EfPilot.Workspace.Configuration;
using EfPilot.Workspace.Discovery;
using Spectre.Console;

var rootCommand = args.FirstOrDefault();

return rootCommand switch
{
    "init" => await RunInitAsync(),
    _ => ShowHelp()
};

static int ShowHelp()
{
    AnsiConsole.MarkupLine("[bold]efpilot[/]");
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("Usage:");
    AnsiConsole.MarkupLine("  [green]efpilot init[/]");

    return 0;
}

static async Task<int> RunInitAsync()
{
    var currentDirectory = Directory.GetCurrentDirectory();

    // 1. Find solution
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

    // 2. Scan projects
    var projectScanner = new ProjectScanner();
    var projects = projectScanner.ScanProjects(solutionDirectory);

    AnsiConsole.MarkupLine($"Found projects: [green]{projects.Count}[/]");

    // 3. Scan DbContexts
    var dbContextScanner = new DbContextScanner();
    var dbContexts = dbContextScanner.ScanDbContexts(projects);

    if (dbContexts.Count == 0)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]No DbContext classes detected. Falling back to manual setup.[/]");
    }
    else
    {
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

    // 4. Create profiles
    var profiles = new List<EfPilotProfile>();

    if (dbContexts.Count > 0)
    {
        var createDetectedProfiles = AnsiConsole.Confirm(
            "Create profiles from detected DbContexts?",
            defaultValue: true);

        if (createDetectedProfiles)
        {
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
                    Project = ToRelativePath(solutionDirectory, dbContext.Project.Path),
                    StartupProject = ToRelativePath(solutionDirectory, selectedCandidate.Project.Path),
                    MigrationsFolder = string.IsNullOrWhiteSpace(migrationsFolder)
                        ? null
                        : migrationsFolder
                });
            }
        }
    }

    if (profiles.Count == 0)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Manual configuration[/]");

        var profileName = AnsiConsole.Ask<string>("Profile name:");
        var dbContextName = AnsiConsole.Ask<string>("DbContext name:");
        var project = AnsiConsole.Ask<string>("Project path:");
        var startupProject = AnsiConsole.Ask<string>("Startup project path:");
        var migrationsFolder = AnsiConsole.Ask<string?>("Migrations folder (optional):");

        profiles.Add(new EfPilotProfile
        {
            Name = profileName,
            DbContext = dbContextName,
            Project = project,
            StartupProject = startupProject,
            MigrationsFolder = string.IsNullOrWhiteSpace(migrationsFolder)
                ? null
                : migrationsFolder
        });
    }
    
    var config = new EfPilotConfig
    {
        Solution = solutionFileName,
        Profiles = profiles
    };

    // 5. Save config
    var store = new EfPilotConfigStore();
    await store.SaveAsync(solutionDirectory, config);

    var configPath = store.GetConfigPath(solutionDirectory);

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[green]efpilot initialized successfully.[/]");
    AnsiConsole.MarkupLine($"Config saved to: [blue]{configPath}[/]");

    return 0;
}

static string ToRelativePath(string baseDirectory, string fullPath)
{
    return Path.GetRelativePath(baseDirectory, fullPath);
}