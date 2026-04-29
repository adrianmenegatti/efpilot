using EfPilot.Cli.Output;
using EfPilot.Core.Abstractions;
using EfPilot.Core.Configuration;
using EfPilot.Core.Migrations;
using Spectre.Console;

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

        var selectedProfiles = ResolveSelectedProfiles(
            context.Config.Profiles,
            profileName,
            all);

        if (selectedProfiles is null)
        {
            return 1;
        }

        foreach (var profile in selectedProfiles)
        {
            var exitCode = await RenderProfileStatusAsync(
                context.SolutionDirectory,
                profile,
                verbose,
                all);

            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        return 0;
    }

    private static List<EfPilotProfile>? ResolveSelectedProfiles(
        IReadOnlyList<EfPilotProfile> profiles,
        string? profileName,
        bool all)
    {
        if (all)
        {
            return profiles.ToList();
        }

        var profile = CommandHelpers.ResolveProfile(profiles, profileName);

        if (profile is null)
        {
            CommandHelpers.PrintProfileNotFound(profiles);
            return null;
        }

        return [profile];
    }

    private async Task<int> RenderProfileStatusAsync(
        string solutionDirectory,
        EfPilotProfile profile,
        bool verbose,
        bool all)
    {
        if (!CommandHelpers.ValidateProfilePaths(solutionDirectory, profile))
        {
            return 1;
        }

        RenderProfileHeader(profile, all);

        var result = await Runner.GetStatusAsync(new MigrationStatusRequest
        {
            SolutionDirectory = solutionDirectory,
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
        ConsoleOutput.BlankLine();

        return 0;
    }

    private static void RenderProfileHeader(EfPilotProfile profile, bool all)
    {
        if (all)
        {
            ConsoleOutput.Header($"{profile.Name} ({profile.DbContext})");
            ConsoleOutput.ProfileSummary(profile);
            ConsoleOutput.BlankLine();
            return;
        }

        ConsoleOutput.CommandIntro("status", profile);
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

        ConsoleOutput.BlankLine();

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