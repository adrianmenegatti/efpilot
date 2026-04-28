using EfPilot.Core.Abstractions;
using EfPilot.Core.Migrations;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

public sealed class AddCommand(IMigrationCommandRunner runner) : MigrationCommand(runner)
{
    public override async Task<int> ExecuteAsync(string[] args)
    {
        var migrationName = args.FirstOrDefault(arg =>
            !arg.StartsWith("--", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(migrationName))
        {
            AnsiConsole.MarkupLine("[red]Migration name is required.[/]");
            AnsiConsole.MarkupLine("Usage: [green]efpilot add <MigrationName> --profile <ProfileName> [--verbose][/]");
            return 1;
        }

        var profileName = CommandHelpers.GetOptionValue(args, "--profile");
        var verbose = CommandHelpers.HasFlag(args, "--verbose");

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
            $"Adding migration [green]{migrationName}[/] using profile [blue]{profile.Name}[/]");

        var result = await Runner.AddMigrationAsync(new AddMigrationRequest
        {
            SolutionDirectory = context.SolutionDirectory,
            Profile = profile,
            MigrationName = migrationName
        });

        if (verbose)
        {
            CommandHelpers.PrintCommandOutput(result.StandardOutput, result.StandardError);
        }

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]✖ Migration failed. Exit code: {result.ExitCode}[/]");

            if (!verbose)
            {
                CommandHelpers.PrintCommandOutput(result.StandardOutput, result.StandardError);
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

        return 0;
    }
}