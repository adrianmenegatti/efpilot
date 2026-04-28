using EfPilot.Core.Configuration;

namespace EfPilot.Core.Migrations;

public sealed class RemoveMigrationRequest
{
    public required string SolutionDirectory { get; init; }

    public required EfPilotProfile Profile { get; init; }

    public bool Force { get; init; }
}