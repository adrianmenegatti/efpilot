using System.Diagnostics;
using EfPilot.Core.Abstractions;
using EfPilot.Core.Migrations;

namespace EfPilot.EfCore.Execution;

public sealed class DotNetEfMigrationCommandRunner : IMigrationCommandRunner
{
    public async Task<MigrationCommandResult> AddMigrationAsync(
        AddMigrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = request.Profile;

        var arguments = new List<string>
        {
            "ef",
            "migrations",
            "add",
            request.MigrationName,
            "--project",
            ToFullPath(request.SolutionDirectory, profile.Project),
            "--startup-project",
            ToFullPath(request.SolutionDirectory, profile.StartupProject),
            "--context",
            profile.DbContext
        };

        if (string.IsNullOrWhiteSpace(profile.MigrationsFolder))
            return await RunDotNetAsync(
                request.SolutionDirectory,
                arguments,
                cancellationToken);
        
        arguments.Add("--output-dir");
        arguments.Add(profile.MigrationsFolder);

        return await RunDotNetAsync(
            request.SolutionDirectory,
            arguments,
            cancellationToken);
    }

    private static async Task<MigrationCommandResult> RunDotNetAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process();
        process.StartInfo = startInfo;

        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        return process.ExitCode == 0
            ? MigrationCommandResult.Succeeded(standardOutput, standardError)
            : MigrationCommandResult.Failed(standardOutput, standardError, process.ExitCode);
    }
    
    public async Task<MigrationCommandResult> RemoveMigrationAsync(
        RemoveMigrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = request.Profile;

        var arguments = new List<string>
        {
            "ef",
            "migrations",
            "remove",
            "--project",
            ToFullPath(request.SolutionDirectory, profile.Project),
            "--startup-project",
            ToFullPath(request.SolutionDirectory, profile.StartupProject),
            "--context",
            profile.DbContext
        };

        if (request.Force)
        {
            arguments.Add("--force");
        }

        return await RunDotNetAsync(
            request.SolutionDirectory,
            arguments,
            cancellationToken);
    }
    
    public async Task<MigrationCommandResult> UpdateDatabaseAsync(
        UpdateDatabaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = request.Profile;

        var arguments = new List<string>
        {
            "ef",
            "database",
            "update"
        };

        if (!string.IsNullOrWhiteSpace(request.TargetMigration))
        {
            arguments.Add(request.TargetMigration);
        }

        arguments.AddRange([
            "--project",
            ToFullPath(request.SolutionDirectory, profile.Project),
            "--startup-project",
            ToFullPath(request.SolutionDirectory, profile.StartupProject),
            "--context",
            profile.DbContext
        ]);

        return await RunDotNetAsync(
            request.SolutionDirectory,
            arguments,
            cancellationToken);
    }

    private static string ToFullPath(string solutionDirectory, string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(solutionDirectory, path));
    }
}