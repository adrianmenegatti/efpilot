using EfPilot.Core.Configuration;

namespace EfPilot.Core.Diagnostics;

public sealed class DiagnosticsRequest
{
    public required string SolutionDirectory { get; init; }

    public EfPilotProfile? Profile { get; init; }

    public bool Verbose { get; init; }
}
