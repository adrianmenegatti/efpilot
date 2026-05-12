using EfPilot.Core.Workspace;

namespace EfPilot.Core.Diagnostics;

public sealed class StartupProjectInfo
{
    public required WorkspaceProject Project { get; init; }

    public required string ProgramPath { get; init; }

    public required bool UsesWebApplicationBuilder { get; init; }

    public required bool UsesGenericHostBuilder { get; init; }

    public required bool CallsDatabaseMigrate { get; init; }
}
