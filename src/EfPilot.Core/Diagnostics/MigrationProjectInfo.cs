using EfPilot.Core.Workspace;

namespace EfPilot.Core.Diagnostics;

public sealed class MigrationProjectInfo
{
    public required WorkspaceProject Project { get; init; }

    public IReadOnlyList<string> ModelSnapshots { get; init; } = [];

    public IReadOnlyList<string> EmptyMigrations { get; init; } = [];

    public bool LooksDedicated =>
        Project.Name.Contains("Migrations", StringComparison.OrdinalIgnoreCase);
}
