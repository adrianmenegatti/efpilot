namespace EfPilot.Cli.Commands;

public abstract class EfPilotCommand
{
    public abstract Task<int> ExecuteAsync(string[] args);
}