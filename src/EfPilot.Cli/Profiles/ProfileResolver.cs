using EfPilot.Core.Configuration;
using Spectre.Console;

namespace EfPilot.Cli.Profiles;

public sealed class ProfileResolver
{
    public EfPilotProfile? Resolve(
        IReadOnlyList<EfPilotProfile> profiles,
        string? profileName)
    {
        if (!string.IsNullOrWhiteSpace(profileName))
        {
            return profiles.FirstOrDefault(profile =>
                string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));
        }

        return profiles.Count switch
        {
            0 => null,
            1 => profiles[0],
            _ => AnsiConsole.Prompt(
                new SelectionPrompt<EfPilotProfile>()
                    .Title("Select profile:")
                    .PageSize(10)
                    .UseConverter(profile => profile.Name)
                    .AddChoices(profiles))
        };
    }

    public void PrintProfileNotFound(IReadOnlyList<EfPilotProfile> profiles)
    {
        AnsiConsole.MarkupLine("[red]Profile not found.[/]");

        if (profiles.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine("Available profiles:");

        foreach (var profile in profiles)
        {
            AnsiConsole.MarkupLine($"- [green]{profile.Name}[/]");
        }
    }
}
