using EfPilot.Core.Abstractions;
using EfPilot.Core.Migrations;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

public sealed class UpdateCommand(IMigrationCommandRunner runner) : MigrationCommand(runner)
{
    public override async Task<int> ExecuteAsync(string[] args)
    {
        var profileName = CommandHelpers.GetOptionValue(args, "--profile");
        var verbose = CommandHelpers.HasFlag(args, "--verbose");
        var targetMigration = CommandHelpers.GetOptionValue(args, "--to");

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

        var result = await Runner.UpdateDatabaseAsync(new UpdateDatabaseRequest
        {
            SolutionDirectory = context.SolutionDirectory,
            Profile = profile,
            TargetMigration = targetMigration
        });

        if (verbose)
        {
            CommandHelpers.PrintCommandOutput(result.StandardOutput, result.StandardError);
        }

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]✖ Database update failed. Exit code: {result.ExitCode}[/]");

            if (!verbose)
            {
                CommandHelpers.PrintCommandOutput(result.StandardOutput, result.StandardError);
            }

            return result.ExitCode;
        }

        AnsiConsole.MarkupLine("[green]✔ Database updated successfully.[/]");
        return 0;
    }
}