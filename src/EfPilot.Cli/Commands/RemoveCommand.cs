using EfPilot.Cli.Output;
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

        ConsoleOutput.Header("EfPilot Remove");
        ConsoleOutput.ProfileSummary(profile);
        AnsiConsole.WriteLine();
        ConsoleOutput.Info("Removing last migration...");
        
        if (force)
        {
            ConsoleOutput.Warning("Force mode enabled.");
        }

        var confirmed = AnsiConsole.Confirm(
            "This will remove the last migration. Continue?",
            defaultValue: false);

        if (!confirmed)
        {
            ConsoleOutput.Warning("Operation cancelled.");
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
            ConsoleOutput.Error($"Remove migration failed. Exit code: {result.ExitCode}");

            if (!verbose)
            {
                CommandHelpers.PrintCommandOutput(result.StandardOutput, result.StandardError);
            }

            return result.ExitCode;
        }

        ConsoleOutput.Success("Last migration removed successfully.");
        return 0;
    }
}