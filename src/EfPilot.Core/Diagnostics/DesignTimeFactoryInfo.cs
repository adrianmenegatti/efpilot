using EfPilot.Core.Workspace;

namespace EfPilot.Core.Diagnostics;

public sealed class DesignTimeFactoryInfo
{
    public required string DbContextName { get; init; }

    public required string FactoryName { get; init; }

    public required WorkspaceProject Project { get; init; }

    public required string Path { get; init; }

    public bool ReadsConfigurationFromCurrentDirectory { get; init; }
}
