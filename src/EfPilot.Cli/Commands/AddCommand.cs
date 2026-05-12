using EfPilot.Cli.Output;
using EfPilot.Cli.Profiles;
using EfPilot.Cli.Workflows;
using EfPilot.Core.Migrations;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

public sealed class AddCommand(
    AddMigrationWorkflow workflow,
    ProfileResolver profileResolver,
    ProfileValidator profileValidator) : EfPilotCommand
{
    public override async Task<int> ExecuteAsync(string[] args)
    {
        var migrationName = args.FirstOrDefault(arg =>
            !arg.StartsWith("--", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(migrationName))
        {
            ConsoleOutput.Error("Migration name is required.");
            AnsiConsole.MarkupLine("Usage: [green]efpilot add <MigrationName> --profile <ProfileName> [[--verbose]][/]");
            return 1;
        }

        var profileName = CommandHelpers.GetOptionValue(args, "--profile");
        var verbose = CommandHelpers.HasFlag(args, "--verbose");

        var workflowResult = await workflow.ExecuteAsync(
            new AddMigrationWorkflowRequest(
                migrationName,
                profileName,
                profile =>
                {
                    ConsoleOutput.CommandIntro("add", profile);
                    ConsoleOutput.Info($"Adding migration '{migrationName}'...");
                }));

        return RenderResult(workflowResult, migrationName, verbose);
    }

    private int RenderResult(
        AddMigrationWorkflowResult workflowResult,
        string migrationName,
        bool verbose)
    {
        switch (workflowResult.Status)
        {
            case AddMigrationWorkflowStatus.ContextUnavailable:
                return 1;

            case AddMigrationWorkflowStatus.ProfileNotFound:
                profileResolver.PrintProfileNotFound(workflowResult.AvailableProfiles);
                return 1;

            case AddMigrationWorkflowStatus.InvalidProfile:
                profileValidator.PrintErrors(workflowResult.ValidationResult!);
                return 1;

            case AddMigrationWorkflowStatus.Completed:
                return RenderMigrationResult(workflowResult.MigrationResult!, migrationName, verbose);

            default:
                throw new InvalidOperationException(
                    $"Unsupported add migration workflow status: {workflowResult.Status}.");
        }
    }

    private static int RenderMigrationResult(
        MigrationCommandResult result,
        string migrationName,
        bool verbose)
    {
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
