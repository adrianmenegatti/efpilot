using System.Reflection;
using EfPilot.Cli.Commands;
using EfPilot.Core.Abstractions;
using EfPilot.Core.Migrations;
using EfPilot.EfCore.Execution;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

var services = new ServiceCollection();

services.AddSingleton<MigrationFileAnalyzer>();
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
    null => ShowHelp(),
    "--help" or "-h" => ShowHelp(),
    "--version" or "-v" => ShowVersion(),
    "init" => await provider.GetRequiredService<InitCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    "add" => await provider.GetRequiredService<AddCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    "remove" => await provider.GetRequiredService<RemoveCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    "update" => await provider.GetRequiredService<UpdateCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    "status" => await provider.GetRequiredService<StatusCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    "diff" => await provider.GetRequiredService<DiffCommand>().ExecuteAsync(args.Skip(1).ToArray()),
    _ => ShowUnknownCommand(rootCommand)
};

static int ShowUnknownCommand(string? command)
{
    AnsiConsole.MarkupLine($"[red]Unknown command: {Markup.Escape(command ?? "")}[/]");
    AnsiConsole.WriteLine();

    return ShowHelp();
}

static int ShowVersion()
{
    var version = Assembly
                      .GetExecutingAssembly()
                      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                      .InformationalVersion
                  ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                  ?? "0.0.1";

    AnsiConsole.MarkupLine($"[green]EfPilot v{Markup.Escape(version)}[/]");

    return 0;
}

static int ShowHelp()
{
    AnsiConsole.MarkupLine("[bold]efpilot[/]");
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("A smarter CLI for managing Entity Framework Core migrations.");
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[yellow]Usage:[/]");
    AnsiConsole.MarkupLine("  [green]efpilot <command>[/]");
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[yellow]Commands:[/]");
    AnsiConsole.MarkupLine("  [green]init[/]      Initialize EfPilot configuration");
    AnsiConsole.MarkupLine("  [green]add[/]       Create a migration");
    AnsiConsole.MarkupLine("  [green]remove[/]    Remove the last migration");
    AnsiConsole.MarkupLine("  [green]update[/]    Apply migrations to the database");
    AnsiConsole.MarkupLine("  [green]status[/]    Show applied and pending migrations");
    AnsiConsole.MarkupLine("  [green]diff[/]      Preview model changes");
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[yellow]Examples:[/]");
    AnsiConsole.MarkupLine("  [green]efpilot init[/]");
    AnsiConsole.MarkupLine("  [green]efpilot add AddLoanCode --profile RateRiskDbContext [[--verbose]][/]");
    AnsiConsole.MarkupLine("  [green]efpilot remove --profile RateRiskDbContext [[--force]] [[--verbose]][/]");
    AnsiConsole.MarkupLine("  [green]efpilot update --profile RateRiskDbContext [[--to InitialCreate]] [[--verbose]][/]");
    AnsiConsole.MarkupLine("  [green]efpilot status --profile RateRiskDbContext [[--all]] [[--verbose]][/]");
    AnsiConsole.MarkupLine("  [green]efpilot diff --profile RateRiskDbContext [[--verbose]][/]");
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[yellow]Global options:[/]");
    AnsiConsole.MarkupLine("  [green]--help, -h[/]       Show help");
    AnsiConsole.MarkupLine("  [green]--version, -v[/]    Show version");

    return 0;
}