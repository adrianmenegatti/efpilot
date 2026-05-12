using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace EfPilot.Cli.Commands;

public sealed class EfPilotCommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<EfPilotCommandDescriptor> _commands;

    public EfPilotCommandDispatcher(
        IServiceProvider serviceProvider,
        IEnumerable<EfPilotCommandDescriptor> commandDescriptors)
    {
        _serviceProvider = serviceProvider;
        _commands = commandDescriptors.ToList();

        var duplicate = _commands
            .GroupBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Multiple efpilot commands are registered with the name '{duplicate.Key}'.");
        }
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        var rootCommand = args.FirstOrDefault();

        return rootCommand switch
        {
            null => ShowHelp(),
            "--help" or "-h" => ShowHelp(),
            "--version" or "-v" => ShowVersion(),
            _ => await ExecuteCommandAsync(rootCommand, args.Skip(1).ToArray())
        };
    }

    private async Task<int> ExecuteCommandAsync(string commandName, string[] args)
    {
        var descriptor = _commands.FirstOrDefault(command =>
            string.Equals(command.Name, commandName, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            return ShowUnknownCommand(commandName);
        }

        var command = (EfPilotCommand)_serviceProvider.GetRequiredService(descriptor.CommandType);

        return await command.ExecuteAsync(args);
    }

    private int ShowUnknownCommand(string? command)
    {
        AnsiConsole.MarkupLine($"[red]Unknown command: {Markup.Escape(command ?? "")}[/]");
        AnsiConsole.WriteLine();

        return ShowHelp();
    }

    private static int ShowVersion()
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

    private int ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]efpilot[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("A smarter CLI for managing Entity Framework Core migrations.");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[yellow]Usage:[/]");
        AnsiConsole.MarkupLine("  [green]efpilot <command>[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[yellow]Commands:[/]");

        var commandNameWidth = _commands.Count == 0
            ? 0
            : _commands.Max(command => command.Name.Length);

        foreach (var command in _commands)
        {
            AnsiConsole.MarkupLine(
                $"  [green]{Markup.Escape(command.Name.PadRight(commandNameWidth))}[/]  " +
                Markup.Escape(command.Description));
        }

        AnsiConsole.WriteLine();

        var examples = _commands
            .SelectMany(command => command.Examples)
            .ToList();

        if (examples.Count > 0)
        {
            AnsiConsole.MarkupLine("[yellow]Examples:[/]");

            foreach (var example in examples)
            {
                AnsiConsole.MarkupLine($"  [green]{Markup.Escape(example)}[/]");
            }

            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("[yellow]Global options:[/]");
        AnsiConsole.MarkupLine("  [green]--help, -h[/]       Show help");
        AnsiConsole.MarkupLine("  [green]--version, -v[/]    Show version");

        return 0;
    }
}
