using System.Diagnostics;
using EfPilot.Core.Abstractions;
using EfPilot.Core.Configuration;
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

        var projectDirectory = GetProjectDirectory(
            request.SolutionDirectory,
            profile);

        var filesBefore = SnapshotProjectFiles(projectDirectory);

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

        if (!string.IsNullOrWhiteSpace(profile.MigrationsFolder))
        {
            arguments.Add("--output-dir");
            arguments.Add(profile.MigrationsFolder);
        }

        var result = await RunDotNetAsync(
            request.SolutionDirectory,
            arguments,
            cancellationToken);

        if (!result.Success)
        {
            return result;
        }

        var createdMigrationFile = FindCreatedMigrationFile(
            projectDirectory,
            filesBefore,
            request.MigrationName);

        if (createdMigrationFile is null)
        {
            return new MigrationCommandResult
            {
                Success = true,
                ExitCode = result.ExitCode,
                StandardOutput = result.StandardOutput,
                StandardError = result.StandardError
            };
        }

        if (!IsEmptyMigration(createdMigrationFile))
        {
            return new MigrationCommandResult
            {
                Success = true,
                ExitCode = result.ExitCode,
                StandardOutput = result.StandardOutput,
                StandardError = result.StandardError,
                CreatedMigrationFile = createdMigrationFile
            };
        }

        var removeResult = await RemoveMigrationAsync(new RemoveMigrationRequest
        {
            SolutionDirectory = request.SolutionDirectory,
            Profile = profile,
            Force = false
        }, cancellationToken);

        return new MigrationCommandResult
        {
            Success = removeResult.Success,
            ExitCode = removeResult.ExitCode,
            StandardOutput = result.StandardOutput + Environment.NewLine + removeResult.StandardOutput,
            StandardError = result.StandardError + Environment.NewLine + removeResult.StandardError,
            NoModelChangesDetected = removeResult.Success,
            CreatedMigrationFile = createdMigrationFile
        };
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
    
    public async Task<MigrationCommandResult> GetStatusAsync(
        MigrationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = request.Profile;

        var arguments = new List<string>
        {
            "ef",
            "migrations",
            "list",
            "--project",
            ToFullPath(request.SolutionDirectory, profile.Project),
            "--startup-project",
            ToFullPath(request.SolutionDirectory, profile.StartupProject),
            "--context",
            profile.DbContext
        };

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

    private static bool IsEmptyMigration(string migrationFile)
    {
        var content = File.ReadAllText(migrationFile);

        return IsMethodBodyEmpty(content, "Up") &&
               IsMethodBodyEmpty(content, "Down");
    }

    private static bool IsMethodBodyEmpty(string content, string methodName)
    {
        var methodIndex = content.IndexOf(
            $"void {methodName}",
            StringComparison.OrdinalIgnoreCase);

        if (methodIndex < 0)
        {
            return false;
        }

        var openBraceIndex = content.IndexOf('{', methodIndex);

        if (openBraceIndex < 0)
        {
            return false;
        }

        var closeBraceIndex = FindMatchingBrace(content, openBraceIndex);

        if (closeBraceIndex < 0)
        {
            return false;
        }

        var body = content[(openBraceIndex + 1)..closeBraceIndex];

        return string.IsNullOrWhiteSpace(RemoveComments(body));
    }

    private static int FindMatchingBrace(string content, int openBraceIndex)
    {
        var depth = 0;

        for (var i = openBraceIndex; i < content.Length; i++)
        {
            if (content[i] == '{')
            {
                depth++;
            }
            else if (content[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string RemoveComments(string value)
    {
        var withoutLineComments = System.Text.RegularExpressions.Regex.Replace(
            value,
            @"//.*?$",
            "",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        return System.Text.RegularExpressions.Regex.Replace(
            withoutLineComments,
            @"/\*.*?\*/",
            "",
            System.Text.RegularExpressions.RegexOptions.Singleline);
    }
    
    private static string GetProjectDirectory(
        string solutionDirectory,
        EfPilotProfile profile)
    {
        var projectPath = ToFullPath(solutionDirectory, profile.Project);
        return Path.GetDirectoryName(projectPath)!;
    }

    private static HashSet<string> SnapshotProjectFiles(string projectDirectory)
    {
        if (!Directory.Exists(projectDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifact(path))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? FindCreatedMigrationFile(
        string projectDirectory,
        HashSet<string> filesBefore,
        string migrationName)
    {
        if (!Directory.Exists(projectDirectory))
        {
            return null;
        }

        var createdFiles = Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifact(path))
            .Select(Path.GetFullPath)
            .Where(path => !filesBefore.Contains(path))
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetCreationTimeUtc)
            .ToList();

        return createdFiles.FirstOrDefault(path =>
                   Path.GetFileName(path).Contains(migrationName, StringComparison.OrdinalIgnoreCase))
               ?? createdFiles.FirstOrDefault();
    }

    private static bool IsBuildArtifact(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts.Any(part =>
            string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase));
    }
}