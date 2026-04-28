namespace EfPilot.Core.Workspace;

public class StartupProjectCandidate
{
    public required WorkspaceProject Project { get; init; }

    public required int Score { get; init; }

    public List<string> Reasons { get; init; } = [];
}