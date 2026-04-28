using EfPilot.Core.Configuration;

namespace EfPilot.Cli.Commands;

public sealed record CommandContext(
    string SolutionDirectory,
    EfPilotConfig Config);