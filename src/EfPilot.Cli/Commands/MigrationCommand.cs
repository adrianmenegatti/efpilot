using EfPilot.Core.Abstractions;

namespace EfPilot.Cli.Commands;

public abstract class MigrationCommand(IMigrationCommandRunner commandRunner) : EfPilotCommand
{
    protected readonly IMigrationCommandRunner Runner = commandRunner;
}