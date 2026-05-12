using EfPilot.Workspace.Configuration;
using EfPilot.Workspace.Discovery;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

public sealed class CommandContextLoader(
    SolutionFinder solutionFinder,
    EfPilotConfigStore configStore)
{
    public async Task<CommandContext?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var solutionPath = solutionFinder.FindSolutionFile(currentDirectory);

        if (solutionPath is null)
        {
            AnsiConsole.MarkupLine("[red]No .sln or .slnx file found.[/]");
            return null;
        }

        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        var config = await configStore.LoadAsync(solutionDirectory, cancellationToken);

        if (config is null)
        {
            AnsiConsole.MarkupLine("[red]efpilot is not initialized for this solution.[/]");
            AnsiConsole.MarkupLine("Run [green]efpilot init[/] first.");
            return null;
        }

        return new CommandContext(solutionDirectory, config);
    }
}