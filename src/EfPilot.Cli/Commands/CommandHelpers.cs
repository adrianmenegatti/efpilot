using Spectre.Console;

namespace EfPilot.Cli.Commands;

internal static class CommandHelpers
{
    public static void PrintCommandOutput(string standardOutput, string standardError)
    {
        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            AnsiConsole.WriteLine(standardOutput);
        }

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            AnsiConsole.MarkupLine("[yellow]stderr:[/]");
            AnsiConsole.WriteLine(standardError);
        }
    }

    public static string? GetOptionValue(string[] args, string optionName)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return i + 1 < args.Length ? args[i + 1] : null;
        }

        return null;
    }

    public static bool HasFlag(string[] args, string flagName)
    {
        return args.Any(arg =>
            string.Equals(arg, flagName, StringComparison.OrdinalIgnoreCase));
    }

    public static string ToRelativePath(string baseDirectory, string fullPath)
    {
        return Path.GetRelativePath(baseDirectory, fullPath);
    }
}
