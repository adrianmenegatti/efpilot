using EfPilot.Core.Abstractions;
using EfPilot.Core.Migrations;
using Spectre.Console;
using EfPilot.Cli.Output;

namespace EfPilot.Cli.Commands;

public sealed class StatusCommand(IMigrationCommandRunner runner) : MigrationCommand(runner)
{
    public override async Task<int> ExecuteAsync(string[] args)
    {
        var profileName = CommandHelpers.GetOptionValue(args, "--profile");
        var verbose = CommandHelpers.HasFlag(args, "--verbose");
        var all = CommandHelpers.HasFlag(args, "--all");

        var context = await CommandHelpers.LoadContextAsync();

        if (context is null)
        {
            return 1;
        }

        var selectedProfiles = new List<EfPilot.Core.Configuration.EfPilotProfile>();

        if (all)
        {
            selectedProfiles.AddRange(context.Config.Profiles);
        }
        else
        {
            var profile = CommandHelpers.ResolveProfile(context.Config.Profiles, profileName);

            if (profile is null)
            {
                CommandHelpers.PrintProfileNotFound(context.Config.Profiles);
                return 1;
            }

            selectedProfiles.Add(profile);
        }

        foreach (var profile in selectedProfiles)
        {
            if (!CommandHelpers.ValidateProfilePaths(context.SolutionDirectory, profile))
            {
                return 1;
            }

            ConsoleOutput.Header(profile.Name);
            ConsoleOutput.ProfileSummary(profile);

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
                ConsoleOutput.Error($"Could not read migration status. Exit code: {result.ExitCode}");

                if (!verbose)
                {
                    CommandHelpers.PrintCommandOutput(result.StandardOutput, result.StandardError);
                }

                return result.ExitCode;
            }

            PrintMigrationStatus(result.StandardOutput);
            AnsiConsole.WriteLine();
        }

        return 0;
    }

private static void PrintMigrationStatus(string standardOutput)
{
    var migrations = standardOutput
        .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(IsMigrationLine)
        .Select(ParseMigrationLine)
        .ToList();

    if (migrations.Count == 0)
    {
        ConsoleOutput.Warning("No migrations found.");
        return;
    }

    var appliedCount = migrations.Count(migration => !migration.IsPending);
    var pendingCount = migrations.Count(migration => migration.IsPending);

    AnsiConsole.MarkupLine(
        $"Migrations: [green]{appliedCount} applied[/], [yellow]{pendingCount} pending[/]");

    AnsiConsole.WriteLine();

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Migration")
        .AddColumn("Status");

    foreach (var migration in migrations)
    {
        table.AddRow(
            Markup.Escape(migration.Name),
            migration.IsPending ? "[yellow]Pending[/]" : "[green]Applied[/]");
    }

    AnsiConsole.Write(table);
}

    private static bool IsMigrationLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (line.StartsWith("Build started", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Build succeeded", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Done.", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("The Entity Framework tools version", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("fixes. See", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return line.Length >= 15 &&
               char.IsDigit(line[0]) &&
               line.Contains('_');
    }

    private static MigrationStatusLine ParseMigrationLine(string line)
    {
        var isPending = line.Contains("Pending", StringComparison.OrdinalIgnoreCase);

        var name = line
            .Replace("(Pending)", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return new MigrationStatusLine(name, isPending);
    }

    private sealed record MigrationStatusLine(string Name, bool IsPending);
}