using EfPilot.Cli.Commands;
using EfPilot.Core.Abstractions;
using EfPilot.EfCore.Execution;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

var services = new ServiceCollection();

services.AddSingleton<IMigrationCommandRunner, DotNetEfMigrationCommandRunner>();

services.AddTransient<InitCommand>();
services.AddTransient<AddCommand>();
services.AddTransient<RemoveCommand>();
services.AddTransient<UpdateCommand>();
services.AddTransient<StatusCommand>();
services.AddTransient<DiffCommand>();

var provider = services.BuildServiceProvider();

var rootCommand = args.FirstOrDefault();

return rootCommand switch
{
    "init" => await provider.GetRequiredService<InitCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    "add" => await provider.GetRequiredService<AddCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    "remove" => await provider.GetRequiredService<RemoveCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    "update" => await provider.GetRequiredService<UpdateCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    "status" => await provider.GetRequiredService<StatusCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    "diff" => await provider.GetRequiredService<DiffCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    _ => ShowHelp()
};

static int ShowHelp()
{
    AnsiConsole.MarkupLine("[bold]efpilot[/]");
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("Usage:");
    AnsiConsole.MarkupLine("  [green]efpilot init[/]");
    AnsiConsole.MarkupLine("  [green]efpilot add <MigrationName> --profile <ProfileName> [--verbose][/]");
    AnsiConsole.MarkupLine("  [green]efpilot remove --profile <ProfileName> [--force] [--verbose][/]");
    AnsiConsole.MarkupLine("  [green]efpilot update --profile <ProfileName> [--to <Migration>] [--verbose][/]");
    AnsiConsole.MarkupLine("  [green]efpilot status --profile <ProfileName> [--verbose][/]");
    AnsiConsole.MarkupLine("  [green]efpilot diff --profile <ProfileName> [--verbose][/]");

    return 0;
}