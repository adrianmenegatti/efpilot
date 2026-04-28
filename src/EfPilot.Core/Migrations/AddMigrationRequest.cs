using EfPilot.Core.Configuration;

namespace EfPilot.Core.Migrations;

public sealed class AddMigrationRequest
{
    public required string SolutionDirectory { get; init; }

    public required EfPilotProfile Profile { get; init; }

    public required string MigrationName { get; init; }
}