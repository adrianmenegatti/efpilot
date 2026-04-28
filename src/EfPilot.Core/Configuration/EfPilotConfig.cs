namespace EfPilot.Core.Configuration;

public class EfPilotConfig
{
    public int Version { get; init; } = 1;

    public required string Solution { get; init; }

    public List<EfPilotProfile> Profiles { get; init; } = [];
}