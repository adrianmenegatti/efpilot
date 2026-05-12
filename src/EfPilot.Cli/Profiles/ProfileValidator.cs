using EfPilot.Core.Configuration;
using Spectre.Console;

namespace EfPilot.Cli.Profiles;

public sealed class ProfileValidator
{
    public ProfileValidationResult ValidatePaths(
        string solutionDirectory,
        EfPilotProfile profile)
    {
        var errors = new List<string>();
        var projectPath = ToFullPath(solutionDirectory, profile.Project);
        var startupProjectPath = ToFullPath(solutionDirectory, profile.StartupProject);

        if (!File.Exists(projectPath))
        {
            errors.Add($"Project file not found: {projectPath}");
        }

        if (!File.Exists(startupProjectPath))
        {
            errors.Add($"Startup project file not found: {startupProjectPath}");
        }

        return errors.Count == 0
            ? ProfileValidationResult.Valid
            : new ProfileValidationResult(errors);
    }

    public void PrintErrors(ProfileValidationResult result)
    {
        foreach (var error in result.Errors)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(error)}[/]");
        }
    }

    private static string ToFullPath(string solutionDirectory, string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(solutionDirectory, path));
    }
}
