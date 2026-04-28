namespace EfPilot.Core.Migrations;

public sealed class MigrationCommandResult
{
    public required bool Success { get; init; }

    public required int ExitCode { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    public static MigrationCommandResult Succeeded(
        string standardOutput,
        string standardError,
        int exitCode = 0)
    {
        return new MigrationCommandResult
        {
            Success = true,
            ExitCode = exitCode,
            StandardOutput = standardOutput,
            StandardError = standardError
        };
    }

    public static MigrationCommandResult Failed(
        string standardOutput,
        string standardError,
        int exitCode)
    {
        return new MigrationCommandResult
        {
            Success = false,
            ExitCode = exitCode,
            StandardOutput = standardOutput,
            StandardError = standardError
        };
    }
}