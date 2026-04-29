using EfPilot.Cli.Output;
using EfPilot.Core.Abstractions;
using EfPilot.Core.Migrations;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

public sealed class DiffCommand(IMigrationCommandRunner runner) : MigrationCommand(runner)
{
    public override async Task<int> ExecuteAsync(string[] args)
    {
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

        var tempMigrationName = $"__EfPilotDiff_{DateTime.UtcNow:yyyyMMddHHmmss}";

        ConsoleOutput.CommandIntro("diff", profile);
        ConsoleOutput.Info("Checking model changes...");

        var addResult = await Runner.AddMigrationAsync(new AddMigrationRequest
        {
            SolutionDirectory = context.SolutionDirectory,
            Profile = profile,
            MigrationName = tempMigrationName
        });

        if (verbose)
        {
            CommandHelpers.PrintCommandOutput(addResult.StandardOutput, addResult.StandardError);
        }

        if (!addResult.Success)
        {
            ConsoleOutput.Error($"Could not generate temporary migration. Exit code: {addResult.ExitCode}");
            
            if (!verbose)
            {
                CommandHelpers.PrintCommandOutput(addResult.StandardOutput, addResult.StandardError);
            }

            return addResult.ExitCode;
        }

        if (addResult.NoModelChangesDetected || string.IsNullOrWhiteSpace(addResult.CreatedMigrationFile))
        {
            ConsoleOutput.Success("No model changes detected.");
            return 0;
        }

        var parser = new MigrationDiffParser();
        var operations = parser.Parse(addResult.CreatedMigrationFile);

        var removeResult = await Runner.RemoveMigrationAsync(new RemoveMigrationRequest
        {
            SolutionDirectory = context.SolutionDirectory,
            Profile = profile,
            Force = false
        });

        if (!removeResult.Success)
        {
            ConsoleOutput.Error("Temporary migration was created but could not be removed.");
            CommandHelpers.PrintCommandOutput(removeResult.StandardOutput, removeResult.StandardError);
            return removeResult.ExitCode;
        }

        if (verbose)
        {
            ConsoleOutput.Success("Temporary migration cleaned up.");
        }

        if (operations.Count == 0)
        {
            ConsoleOutput.Warning("Changes detected, but no migration operations could be parsed.");
            return 0;
        }

        PrintDiff(operations);

        return 0;
    }

    private static void PrintDiff(IReadOnlyList<MigrationOperationSummary> operations)
    {
        ConsoleOutput.Warning("Model changes detected.");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Operation")
            .AddColumn("Description");

        foreach (var operation in operations)
        {
            table.AddRow(
                Markup.Escape(operation.Operation),
                Markup.Escape(operation.Description));
        }

        AnsiConsole.Write(table);
    }
}