using EfPilot.Cli.Output;
using EfPilot.Cli.Profiles;
using EfPilot.Core.Abstractions;
using EfPilot.Core.Configuration;
using EfPilot.Core.Diagnostics;
using EfPilot.Core.Migrations;
using EfPilot.Workspace.Diagnostics;
using EfPilot.Workspace.Discovery;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

public sealed class StatusCommand(
    IMigrationCommandRunner runner,
    CommandContextLoader contextLoader,
    ProfileResolver profileResolver,
    ProfileValidator profileValidator,
    ProjectScanner projectScanner,
    DesignTimeFactoryAnalyzer designTimeFactoryAnalyzer) : MigrationCommand(runner)
{
    public override async Task<int> ExecuteAsync(string[] args)
    {
        var profileName = CommandHelpers.GetOptionValue(args, "--profile");
        var verbose = CommandHelpers.HasFlag(args, "--verbose");
        var all = CommandHelpers.HasFlag(args, "--all");

        var context = await contextLoader.LoadAsync();

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

        var designTimeFactories = DiscoverDesignTimeFactories(context.SolutionDirectory);

        foreach (var profile in selectedProfiles)
        {
            var designTimeFactoryPreference = GetDesignTimeFactoryPreference(
                context.SolutionDirectory,
                profile,
                designTimeFactories);

            var exitCode = await RenderProfileStatusAsync(
                context.SolutionDirectory,
                profile,
                verbose,
                all,
                designTimeFactoryPreference);

            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        return 0;
    }

    private List<EfPilotProfile>? ResolveSelectedProfiles(
        IReadOnlyList<EfPilotProfile> profiles,
        string? profileName,
        bool all)
    {
        if (all)
        {
            return profiles.ToList();
        }

        var profile = profileResolver.Resolve(profiles, profileName);

        if (profile is null)
        {
            profileResolver.PrintProfileNotFound(profiles);
            return null;
        }

        return [profile];
    }

    private async Task<int> RenderProfileStatusAsync(
        string solutionDirectory,
        EfPilotProfile profile,
        bool verbose,
        bool all,
        DesignTimeFactoryPreference designTimeFactoryPreference)
    {
        var validation = profileValidator.ValidatePaths(solutionDirectory, profile);

        if (!validation.IsValid)
        {
            profileValidator.PrintErrors(validation);
            return 1;
        }

        var statusExecution = await GetStatusWithFactoryFallbackAsync(
            solutionDirectory,
            profile,
            designTimeFactoryPreference);

        var result = statusExecution.Result;
        
        var migrations = ParseMigrations(result.StandardOutput);

        var appliedCount = migrations.Count(m => !m.IsPending);
        var pendingCount = migrations.Count(m => m.IsPending);

        RenderProfileHeader(profile, all, appliedCount, pendingCount);

        if (verbose)
        {
            PrintStatusExecutionNotes(statusExecution);
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

        PrintMigrationStatus(migrations);
        ConsoleOutput.BlankLine();

        return 0;
    }

    private async Task<StatusExecution> GetStatusWithFactoryFallbackAsync(
        string solutionDirectory,
        EfPilotProfile profile,
        DesignTimeFactoryPreference designTimeFactoryPreference)
    {
        if (!designTimeFactoryPreference.CanTryWithoutStartupProject)
        {
            return new StatusExecution(
                await GetStatusAsync(solutionDirectory, profile, useStartupProject: true),
                TriedWithoutStartupProject: false,
                UsedStartupProjectFallback: false,
                SkippedReason: designTimeFactoryPreference.SkipReason,
                FailedDesignTimeResult: null);
        }

        var designTimeResult = await GetStatusAsync(
            solutionDirectory,
            profile,
            useStartupProject: false);

        if (designTimeResult.Success)
        {
            return new StatusExecution(
                designTimeResult,
                TriedWithoutStartupProject: true,
                UsedStartupProjectFallback: false,
                SkippedReason: null,
                FailedDesignTimeResult: null);
        }

        return new StatusExecution(
            await GetStatusAsync(solutionDirectory, profile, useStartupProject: true),
            TriedWithoutStartupProject: true,
            UsedStartupProjectFallback: true,
            SkippedReason: null,
            FailedDesignTimeResult: designTimeResult);
    }

    private static void PrintStatusExecutionNotes(StatusExecution execution)
    {
        if (!execution.TriedWithoutStartupProject)
        {
            if (!string.IsNullOrWhiteSpace(execution.SkippedReason))
            {
                ConsoleOutput.Info(execution.SkippedReason);
            }

            return;
        }

        if (execution.UsedStartupProjectFallback)
        {
            ConsoleOutput.Warning("Design-time factory path failed. Used configured startup project fallback.");

            if (execution.FailedDesignTimeResult is not null)
            {
                CommandHelpers.PrintCommandOutput(
                    execution.FailedDesignTimeResult.StandardOutput,
                    execution.FailedDesignTimeResult.StandardError);
            }

            return;
        }

        ConsoleOutput.Success("Design-time factory path used. Startup project was not required.");
    }

    private async Task<MigrationCommandResult> GetStatusAsync(
        string solutionDirectory,
        EfPilotProfile profile,
        bool useStartupProject)
    {
        return await Runner.GetStatusAsync(new MigrationStatusRequest
        {
            SolutionDirectory = solutionDirectory,
            Profile = profile,
            UseStartupProject = useStartupProject
        });
    }

    private IReadOnlyList<DesignTimeFactoryInfo> DiscoverDesignTimeFactories(
        string solutionDirectory)
    {
        var projects = projectScanner.ScanProjects(solutionDirectory);

        return designTimeFactoryAnalyzer.Analyze(projects);
    }

    private static DesignTimeFactoryPreference GetDesignTimeFactoryPreference(
        string solutionDirectory,
        EfPilotProfile profile,
        IReadOnlyList<DesignTimeFactoryInfo> designTimeFactories)
    {
        var profileProjectPath = ToFullPath(solutionDirectory, profile.Project);

        var factory = designTimeFactories.FirstOrDefault(factory =>
            string.Equals(factory.DbContextName, profile.DbContext, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                Path.GetFullPath(factory.Project.Path),
                profileProjectPath,
                StringComparison.OrdinalIgnoreCase));

        if (factory is null)
        {
            return new DesignTimeFactoryPreference(
                CanTryWithoutStartupProject: false,
                SkipReason: null);
        }

        if (factory.ReadsConfigurationFromCurrentDirectory)
        {
            return new DesignTimeFactoryPreference(
                CanTryWithoutStartupProject: false,
                SkipReason: "Design-time factory reads appsettings from the current directory. Using configured startup project.");
        }

        return new DesignTimeFactoryPreference(
            CanTryWithoutStartupProject: true,
            SkipReason: null);
    }

    private static string ToFullPath(string solutionDirectory, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(solutionDirectory, path));
    }

    private static void RenderProfileHeader(
        EfPilotProfile profile,
        bool all,
        int applied,
        int pending)
    {
        if (all)
        {
            ConsoleOutput.ProfileHeaderWithStats(
                profile.Name,
                profile.DbContext,
                applied,
                pending);

            ConsoleOutput.ProfileSummary(profile);
            ConsoleOutput.BlankLine();
            return;
        }

        ConsoleOutput.CommandIntro("status", profile);

        AnsiConsole.MarkupLine(
            $"[green]✔ Applied: {applied}[/] | [yellow]⏳ Pending: {pending}[/]");

        ConsoleOutput.BlankLine();
    }
    private static void PrintMigrationStatus(List<MigrationStatusLine> migrations)
    {
        if (migrations.Count == 0)
        {
            ConsoleOutput.Warning("No migrations found.");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Migration")
            .AddColumn("Status");

        foreach (var migration in migrations)
        {
            table.AddRow(
                Markup.Escape(migration.Name),
                migration.IsPending ? "[yellow]⏳ Pending[/]" : "[green]✔ Applied[/]");
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
    
    private static List<MigrationStatusLine> ParseMigrations(string standardOutput)
    {
        return standardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsMigrationLine)
            .Select(ParseMigrationLine)
            .ToList();
    }

    private sealed record MigrationStatusLine(string Name, bool IsPending);

    private sealed record StatusExecution(
        MigrationCommandResult Result,
        bool TriedWithoutStartupProject,
        bool UsedStartupProjectFallback,
        string? SkippedReason,
        MigrationCommandResult? FailedDesignTimeResult);

    private sealed record DesignTimeFactoryPreference(
        bool CanTryWithoutStartupProject,
        string? SkipReason);
}
