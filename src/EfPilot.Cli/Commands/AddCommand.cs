using EfPilot.Cli.Output;
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
            ConsoleOutput.Error("Migration name is required.");
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

        ConsoleOutput.CommandIntro("add", profile);
        ConsoleOutput.Info($"Adding migration '{migrationName}'...");

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
            ConsoleOutput.Error($"Migration failed. Exit code: {result.ExitCode}");

            if (!verbose)
            {
                CommandHelpers.PrintCommandOutput(result.StandardOutput, result.StandardError);
            }

            return result.ExitCode;
        }

        if (result.NoModelChangesDetected)
        {
            ConsoleOutput.Success("No model changes detected. Migration skipped.");
            return 0;
        }

        ConsoleOutput.Success($"Migration '{migrationName}' created successfully.");

        if (!string.IsNullOrWhiteSpace(result.CreatedMigrationFile))
        {
            AnsiConsole.MarkupLine($"[grey]File: {Markup.Escape(result.CreatedMigrationFile)}[/]");
        }

        return 0;
    }
}