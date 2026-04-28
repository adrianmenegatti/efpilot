using EfPilot.Core.Configuration;
using EfPilot.Core.Migrations;
using EfPilot.Core.Profiles;
using EfPilot.Core.Workspace;
using EfPilot.EfCore.Execution;
using EfPilot.Workspace.Configuration;
using EfPilot.Workspace.Discovery;
using Spectre.Console;

var rootCommand = args.FirstOrDefault();

return rootCommand switch
{
    "init" => await RunInitAsync(),
    "add" => await RunAddAsync(args.Skip(1).ToArray()),
    "remove" => await RunRemoveAsync(args.Skip(1).ToArray()),
    "update" => await RunUpdateAsync(args.Skip(1).ToArray()),
    "status" => await RunStatusAsync(args.Skip(1).ToArray()),
    _ => ShowHelp()
};

static int ShowHelp()
{
    AnsiConsole.MarkupLine("[bold]efpilot[/]");
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("Usage:");
    AnsiConsole.MarkupLine("  [green]efpilot init[/]");
    AnsiConsole.MarkupLine("  [green]efpilot add <MigrationName> --profile <ProfileName> [--verbose][/]");
    AnsiConsole.MarkupLine("  [green]efpilot remove --profile <ProfileName> [--force] [--verbose][/]");
    AnsiConsole.MarkupLine("  [green]efpilot update --profile <ProfileName> [--to <Migration>] [--verbose][/]");
    AnsiConsole.MarkupLine("  [green]efpilot status --profile <ProfileName> [--verbose][/]");

    return 0;
}

static async Task<int> RunInitAsync()
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

    var store = new EfPilotConfigStore();
    await store.SaveAsync(solutionDirectory, config);

    var configPath = store.GetConfigPath(solutionDirectory);

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[green]efpilot initialized successfully.[/]");
    AnsiConsole.MarkupLine($"Config saved to: [blue]{configPath}[/]");

    return 0;
}

static async Task<int> RunAddAsync(string[] args)
{
    var migrationName = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.OrdinalIgnoreCase));

    if (string.IsNullOrWhiteSpace(migrationName))
    {
        AnsiConsole.MarkupLine("[red]Migration name is required.[/]");
        AnsiConsole.MarkupLine("Usage: [green]efpilot add <MigrationName> --profile <ProfileName> [--verbose][/]");
        return 1;
    }

    var profileName = GetOptionValue(args, "--profile");
    var verbose = HasFlag(args, "--verbose");

    var context = await LoadExecutionContextAsync();

    if (context is null)
    {
        return 1;
    }

    var profile = ResolveProfile(context.Config.Profiles, profileName);

    if (profile is null)
    {
        PrintProfileNotFound(context.Config.Profiles);
        return 1;
    }

    if (!ValidateProfilePaths(context.SolutionDirectory, profile))
    {
        return 1;
    }

    AnsiConsole.MarkupLine(
        $"Adding migration [green]{migrationName}[/] using profile [blue]{profile.Name}[/]");

    var runner = new DotNetEfMigrationCommandRunner();

    var result = await runner.AddMigrationAsync(new AddMigrationRequest
    {
        SolutionDirectory = context.SolutionDirectory,
        Profile = profile,
        MigrationName = migrationName
    });

    if (verbose)
    {
        PrintCommandOutput(result.StandardOutput, result.StandardError);
    }

    if (!result.Success)
    {
        AnsiConsole.MarkupLine($"[red]✖ Migration failed. Exit code: {result.ExitCode}[/]");

        if (!verbose)
        {
            PrintCommandOutput(result.StandardOutput, result.StandardError);
        }

        return result.ExitCode;
    }

    if (result.NoModelChangesDetected)
    {
        AnsiConsole.MarkupLine("[yellow]✔ No model changes detected. Migration skipped.[/]");
        return 0;
    }

    AnsiConsole.MarkupLine($"[green]✔ Migration '{migrationName}' created successfully.[/]");

    if (!string.IsNullOrWhiteSpace(result.CreatedMigrationFile))
    {
        AnsiConsole.MarkupLine($"[grey]File: {result.CreatedMigrationFile}[/]");
    }

    return 0;}

static async Task<int> RunRemoveAsync(string[] args)
{
    var profileName = GetOptionValue(args, "--profile");
    var verbose = HasFlag(args, "--verbose");
    var force = HasFlag(args, "--force");

    var context = await LoadExecutionContextAsync();

    if (context is null)
    {
        return 1;
    }

    var profile = ResolveProfile(context.Config.Profiles, profileName);

    if (profile is null)
    {
        PrintProfileNotFound(context.Config.Profiles);
        return 1;
    }

    if (!ValidateProfilePaths(context.SolutionDirectory, profile))
    {
        return 1;
    }

    AnsiConsole.MarkupLine(
        $"Removing last migration using profile [blue]{profile.Name}[/]");

    if (force)
    {
        AnsiConsole.MarkupLine("[yellow]Force mode enabled.[/]");
    }

    var confirmed = AnsiConsole.Confirm(
        "This will remove the last migration. Continue?",
        defaultValue: false);

    if (!confirmed)
    {
        AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
        return 0;
    }

    var runner = new DotNetEfMigrationCommandRunner();

    var result = await runner.RemoveMigrationAsync(new RemoveMigrationRequest
    {
        SolutionDirectory = context.SolutionDirectory,
        Profile = profile,
        Force = force
    });

    if (verbose)
    {
        PrintCommandOutput(result.StandardOutput, result.StandardError);
    }

    if (!result.Success)
    {
        AnsiConsole.MarkupLine($"[red]✖ Remove migration failed. Exit code: {result.ExitCode}[/]");

        if (!verbose)
        {
            PrintCommandOutput(result.StandardOutput, result.StandardError);
        }

        return result.ExitCode;
    }

    AnsiConsole.MarkupLine("[green]✔ Last migration removed successfully.[/]");
    return 0;
}

static async Task<int> RunUpdateAsync(string[] args)
{
    var profileName = GetOptionValue(args, "--profile");
    var verbose = HasFlag(args, "--verbose");
    var targetMigration = GetOptionValue(args, "--to");

    var context = await LoadExecutionContextAsync();

    if (context is null)
    {
        return 1;
    }

    var profile = ResolveProfile(context.Config.Profiles, profileName);

    if (profile is null)
    {
        PrintProfileNotFound(context.Config.Profiles);
        return 1;
    }

    if (!ValidateProfilePaths(context.SolutionDirectory, profile))
    {
        return 1;
    }

    if (string.IsNullOrWhiteSpace(targetMigration))
    {
        AnsiConsole.MarkupLine(
            $"Updating database using profile [blue]{profile.Name}[/]");
    }
    else
    {
        AnsiConsole.MarkupLine(
            $"Updating database to migration [green]{targetMigration}[/] using profile [blue]{profile.Name}[/]");
    }

    var runner = new DotNetEfMigrationCommandRunner();

    var result = await runner.UpdateDatabaseAsync(new UpdateDatabaseRequest
    {
        SolutionDirectory = context.SolutionDirectory,
        Profile = profile,
        TargetMigration = targetMigration
    });

    if (verbose)
    {
        PrintCommandOutput(result.StandardOutput, result.StandardError);
    }

    if (!result.Success)
    {
        AnsiConsole.MarkupLine($"[red]✖ Database update failed. Exit code: {result.ExitCode}[/]");

        if (!verbose)
        {
            PrintCommandOutput(result.StandardOutput, result.StandardError);
        }

        return result.ExitCode;
    }

    AnsiConsole.MarkupLine("[green]✔ Database updated successfully.[/]");
    return 0;
}

static async Task<int> RunStatusAsync(string[] args)
{
    var profileName = GetOptionValue(args, "--profile");
    var verbose = HasFlag(args, "--verbose");

    var context = await LoadExecutionContextAsync();

    if (context is null)
    {
        return 1;
    }

    var profile = ResolveProfile(context.Config.Profiles, profileName);

    if (profile is null)
    {
        PrintProfileNotFound(context.Config.Profiles);
        return 1;
    }

    if (!ValidateProfilePaths(context.SolutionDirectory, profile))
    {
        return 1;
    }

    AnsiConsole.MarkupLine($"[bold]efpilot status[/]");
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine($"Profile: [blue]{profile.Name}[/]");
    AnsiConsole.MarkupLine($"DbContext: [green]{profile.DbContext}[/]");
    AnsiConsole.MarkupLine($"Project: [grey]{profile.Project}[/]");
    AnsiConsole.MarkupLine($"Startup: [grey]{profile.StartupProject}[/]");

    if (!string.IsNullOrWhiteSpace(profile.MigrationsFolder))
    {
        AnsiConsole.MarkupLine($"Migrations folder: [grey]{profile.MigrationsFolder}[/]");
    }

    AnsiConsole.WriteLine();

    var runner = new DotNetEfMigrationCommandRunner();

    var result = await runner.GetStatusAsync(new MigrationStatusRequest
    {
        SolutionDirectory = context.SolutionDirectory,
        Profile = profile
    });

    if (verbose)
    {
        PrintCommandOutput(result.StandardOutput, result.StandardError);
    }

    if (!result.Success)
    {
        AnsiConsole.MarkupLine($"[red]✖ Could not read migration status. Exit code: {result.ExitCode}[/]");

        if (!verbose)
        {
            PrintCommandOutput(result.StandardOutput, result.StandardError);
        }

        return result.ExitCode;
    }

    PrintMigrationStatus(result.StandardOutput);

    return 0;
}

static async Task<ExecutionContextData?> LoadExecutionContextAsync()
{
    var currentDirectory = Directory.GetCurrentDirectory();

    var solutionFinder = new SolutionFinder();
    var solutionPath = solutionFinder.FindSolutionFile(currentDirectory);

    if (solutionPath is null)
    {
        AnsiConsole.MarkupLine("[red]No .sln or .slnx file found.[/]");
        return null;
    }

    var solutionDirectory = Path.GetDirectoryName(solutionPath)!;

    var store = new EfPilotConfigStore();
    var config = await store.LoadAsync(solutionDirectory);

    if (config is null)
    {
        AnsiConsole.MarkupLine("[red]efpilot is not initialized for this solution.[/]");
        AnsiConsole.MarkupLine("Run [green]efpilot init[/] first.");
        return null;
    }

    return new ExecutionContextData(solutionDirectory, config);
}

static bool ValidateProfilePaths(string solutionDirectory, EfPilotProfile profile)
{
    var projectPath = ToFullPath(solutionDirectory, profile.Project);
    var startupProjectPath = ToFullPath(solutionDirectory, profile.StartupProject);

    var isValid = true;

    if (!File.Exists(projectPath))
    {
        AnsiConsole.MarkupLine($"[red]Project file not found:[/] {projectPath}");
        isValid = false;
    }

    if (!File.Exists(startupProjectPath))
    {
        AnsiConsole.MarkupLine($"[red]Startup project file not found:[/] {startupProjectPath}");
        isValid = false;
    }

    return isValid;
}

static void PrintCommandOutput(string standardOutput, string standardError)
{
    if (!string.IsNullOrWhiteSpace(standardOutput))
    {
        AnsiConsole.WriteLine(standardOutput);
    }

    if (!string.IsNullOrWhiteSpace(standardError))
    {
        AnsiConsole.MarkupLine("[yellow]stderr:[/]");
        AnsiConsole.WriteLine(standardError);
    }
}

static string? GetOptionValue(string[] args, string optionName)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        return i + 1 < args.Length
            ? args[i + 1]
            : null;
    }

    return null;
}

static bool HasFlag(string[] args, string flagName)
{
    return args.Any(arg => string.Equals(arg, flagName, StringComparison.OrdinalIgnoreCase));
}

static EfPilotProfile? ResolveProfile(
    IReadOnlyList<EfPilotProfile> profiles,
    string? profileName)
{
    if (!string.IsNullOrWhiteSpace(profileName))
    {
        return profiles.FirstOrDefault(profile =>
            string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));
    }

    return profiles.Count switch
    {
        1 => profiles[0],
        _ => AnsiConsole.Prompt(
            new SelectionPrompt<EfPilotProfile>()
                .Title("Select profile:")
                .PageSize(10)
                .UseConverter(profile => profile.Name)
                .AddChoices(profiles))
    };
}

static void PrintProfileNotFound(IReadOnlyList<EfPilotProfile> profiles)
{
    AnsiConsole.MarkupLine("[red]Profile not found.[/]");

    if (profiles.Count == 0)
    {
        return;
    }

    AnsiConsole.MarkupLine("Available profiles:");

    foreach (var profile in profiles)
    {
        AnsiConsole.MarkupLine($"- [green]{profile.Name}[/]");
    }
}

static string ToRelativePath(string baseDirectory, string fullPath)
{
    return Path.GetRelativePath(baseDirectory, fullPath);
}

static string ToFullPath(string solutionDirectory, string path)
{
    return Path.IsPathRooted(path)
        ? path
        : Path.GetFullPath(Path.Combine(solutionDirectory, path));
}

static void PrintMigrationStatus(string standardOutput)
{
    var lines = standardOutput
        .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(line => !line.StartsWith("Build started", StringComparison.OrdinalIgnoreCase))
        .Where(line => !line.StartsWith("Build succeeded", StringComparison.OrdinalIgnoreCase))
        .Where(line => !line.StartsWith("Done.", StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (lines.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No migrations found.[/]");
        return;
    }

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Migration")
        .AddColumn("Status");

    foreach (var line in lines)
    {
        var isPending = line.Contains("Pending", StringComparison.OrdinalIgnoreCase);

        var cleanLine = line
            .Replace("(Pending)", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        table.AddRow(
            Markup.Escape(cleanLine),
            isPending ? "[yellow]Pending[/]" : "[green]Applied[/]");
    }

    AnsiConsole.Write(table);
}

internal sealed record ExecutionContextData(
    string SolutionDirectory,
    EfPilotConfig Config);