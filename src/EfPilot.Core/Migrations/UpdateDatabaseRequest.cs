using EfPilot.Core.Configuration;

namespace EfPilot.Core.Migrations;

public sealed class UpdateDatabaseRequest
{
    public required string SolutionDirectory { get; init; }

    public required EfPilotProfile Profile { get; init; }

    public string? TargetMigration { get; init; }
}