using EfPilot.Core.Configuration;
using Spectre.Console;

namespace EfPilot.Cli.Output;

internal static class ConsoleOutput
{
    public static void Header(string title)
    {
        AnsiConsole.Write(new Rule($"[bold blue]{Markup.Escape(title)}[/]").RuleStyle("grey"));
    }

    public static void CommandHeader(string commandName)
    {
        Header($"efpilot {commandName}");
    }

    public static void Success(string message)
    {
        AnsiConsole.MarkupLine($"[green]✔ {Markup.Escape(message)}[/]");
    }

    public static void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]✖ {Markup.Escape(message)}[/]");
    }

    public static void Warning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠ {Markup.Escape(message)}[/]");
    }

    public static void Info(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ {Markup.Escape(message)}[/]");
    }

    public static void BlankLine()
    {
        AnsiConsole.WriteLine();
    }

    public static void ProfileSummary(EfPilotProfile profile)
    {
        AnsiConsole.MarkupLine($"Profile: [blue]{Markup.Escape(profile.Name)}[/]");
        AnsiConsole.MarkupLine($"DbContext: [green]{Markup.Escape(profile.DbContext)}[/]");
        AnsiConsole.MarkupLine($"Project: [grey]{Markup.Escape(profile.Project)}[/]");
        AnsiConsole.MarkupLine($"Startup: [grey]{Markup.Escape(profile.StartupProject)}[/]");

        if (!string.IsNullOrWhiteSpace(profile.MigrationsFolder))
        {
            AnsiConsole.MarkupLine($"Migrations folder: [grey]{Markup.Escape(profile.MigrationsFolder)}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("Migrations folder: [grey](EF default)[/]");
        }
    }

    public static void CommandIntro(string commandName, EfPilotProfile profile)
    {
        CommandHeader(commandName);
        ProfileSummary(profile);
        BlankLine();
    }
    
    public static void ProfileHeaderWithStats(
        string profileName,
        string dbContext,
        int applied,
        int pending)
    {
        var hasPending = pending > 0;

        var statusIcon = hasPending ? "⏳" : "✔";
        var ruleColor = hasPending ? "yellow" : "green";

        var title =
            $"{statusIcon} [bold]{Markup.Escape(profileName)}[/] " +
            $"[grey]({Markup.Escape(dbContext)})[/] " +
            $"[green]✔ Applied: {applied}[/] | " +
            $"[yellow]⏳ Pending: {pending}[/]";

        AnsiConsole.Write(
            new Rule(title)
                .RuleStyle(ruleColor));
    }
}