using EfPilot.Core.Configuration;
using EfPilot.Workspace.Configuration;
using EfPilot.Workspace.Discovery;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

internal static class CommandHelpers
{
    public static async Task<CommandContext?> LoadContextAsync()
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

        return new CommandContext(solutionDirectory, config);
    }

    public static EfPilotProfile? ResolveProfile(
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

    public static bool ValidateProfilePaths(string solutionDirectory, EfPilotProfile profile)
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

    public static void PrintProfileNotFound(IReadOnlyList<EfPilotProfile> profiles)
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

    public static void PrintCommandOutput(string standardOutput, string standardError)
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

    public static string? GetOptionValue(string[] args, string optionName)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return i + 1 < args.Length ? args[i + 1] : null;
        }

        return null;
    }

    public static bool HasFlag(string[] args, string flagName)
    {
        return args.Any(arg =>
            string.Equals(arg, flagName, StringComparison.OrdinalIgnoreCase));
    }

    public static string ToRelativePath(string baseDirectory, string fullPath)
    {
        return Path.GetRelativePath(baseDirectory, fullPath);
    }

    public static string ToFullPath(string solutionDirectory, string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(solutionDirectory, path));
    }
}