using EfPilot.Core.Abstractions;
using EfPilot.Core.Migrations;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

public sealed class StatusCommand(IMigrationCommandRunner runner) : MigrationCommand(runner)
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

        AnsiConsole.MarkupLine("[bold]efpilot status[/]");
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

        var result = await Runner.GetStatusAsync(new MigrationStatusRequest
        {
            SolutionDirectory = context.SolutionDirectory,
            Profile = profile
        });

        if (verbose)
        {
            CommandHelpers.PrintCommandOutput(result.StandardOutput, result.StandardError);
        }

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]✖ Could not read migration status. Exit code: {result.ExitCode}[/]");

            if (!verbose)
            {
                CommandHelpers.PrintCommandOutput(result.StandardOutput, result.StandardError);
            }

            return result.ExitCode;
        }

        PrintMigrationStatus(result.StandardOutput);

        return 0;
    }

    private static void PrintMigrationStatus(string standardOutput)
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
}