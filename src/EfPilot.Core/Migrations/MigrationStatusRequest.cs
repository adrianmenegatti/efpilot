using EfPilot.Core.Configuration;

namespace EfPilot.Core.Migrations;

public sealed class MigrationStatusRequest
{
    public required string SolutionDirectory { get; init; }

    public required EfPilotProfile Profile { get; init; }
}