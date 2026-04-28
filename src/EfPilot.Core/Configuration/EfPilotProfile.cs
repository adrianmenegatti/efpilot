namespace EfPilot.Core.Configuration;

public class EfPilotProfile
{
    public required string Name { get; init; }

    public required string DbContext { get; init; }

    public required string Project { get; init; }

    public required string StartupProject { get; init; }

    public string? MigrationsFolder { get; init; }
}