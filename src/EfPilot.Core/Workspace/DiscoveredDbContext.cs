namespace EfPilot.Core.Workspace;

public class DiscoveredDbContext
{
    public required string Name { get; init; }

    public required WorkspaceProject Project { get; init; }
}