using EfPilot.Core.Abstractions;
using EfPilot.Core.Migrations;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

public sealed class RemoveCommand(IMigrationCommandRunner runner) : MigrationCommand(runner)
{
    public override async Task<int> ExecuteAsync(string[] args)
    {
        var profileName = CommandHelpers.GetOptionValue(args, "--profile");
        var verbose = CommandHelpers.HasFlag(args, "--verbose");
        var force = CommandHelpers.HasFlag(args, "--force");

        var context = await CommandHelpers.LoadContextAsync();

        if (context is null)
        {
            return 1;
        }

        var profile = CommandHelpers.ResolveProfile(context.Config.Profiles, profileName);

        if (profile is null)
        {
            CommandHelpers.PrintProfileNotFound(context.Config.Profiles);
            return 1;
        }

        if (!CommandHelpers.ValidateProfilePaths(context.SolutionDirectory, profile))
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

        var result = await Runner.RemoveMigrationAsync(new RemoveMigrationRequest
        {
            SolutionDirectory = context.SolutionDirectory,
            Profile = profile,
            Force = force
        });

        if (verbose)
        {
            CommandHelpers.PrintCommandOutput(result.StandardOutput, result.StandardError);
        }

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]✖ Remove migration failed. Exit code: {result.ExitCode}[/]");

            if (!verbose)
            {
                CommandHelpers.PrintCommandOutput(result.StandardOutput, result.StandardError);
            }

            return result.ExitCode;
        }

        AnsiConsole.MarkupLine("[green]✔ Last migration removed successfully.[/]");
        return 0;
    }
}