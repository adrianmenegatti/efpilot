using System.Text.RegularExpressions;

namespace EfPilot.Core.Migrations;

public sealed partial class MigrationDiffParser
{
    public IReadOnlyList<MigrationOperationSummary> Parse(string migrationFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationFilePath);

        var content = File.ReadAllText(migrationFilePath);
        var upBody = ExtractMethodBody(content, "Up");

        if (string.IsNullOrWhiteSpace(upBody))
        {
            return [];
        }

        var operations = new List<MigrationOperationSummary>();

        foreach (Match match in MigrationBuilderOperationRegex().Matches(upBody))
        {
            var operation = match.Groups["operation"].Value;
            var args = match.Groups["args"].Value;

            operations.Add(new MigrationOperationSummary(
                operation,
                BuildDescription(operation, args)));
        }

        return operations;
    }

    private static string BuildDescription(string operation, string args)
    {
        var table = ExtractNamedArgument(args, "table");
        var name = ExtractNamedArgument(args, "name");

        return operation switch
        {
            "CreateTable" when name is not null =>
                $"Create table '{name}'",

            "DropTable" when name is not null =>
                $"Drop table '{name}'",

            "AddColumn" when table is not null && name is not null =>
                $"Add column '{name}' to '{table}'",

            "DropColumn" when table is not null && name is not null =>
                $"Drop column '{name}' from '{table}'",

            "AlterColumn" when table is not null && name is not null =>
                $"Alter column '{name}' in '{table}'",

            "CreateIndex" when table is not null && name is not null =>
                $"Create index '{name}' on '{table}'",

            "DropIndex" when table is not null && name is not null =>
                $"Drop index '{name}' from '{table}'",

            "AddForeignKey" when table is not null && name is not null =>
                $"Add foreign key '{name}' on '{table}'",

            "DropForeignKey" when table is not null && name is not null =>
                $"Drop foreign key '{name}' from '{table}'",

            _ => operation
        };
    }

    private static string? ExtractNamedArgument(string args, string argumentName)
    {
        var pattern = $@"{argumentName}\s*:\s*""(?<value>[^""]+)""";
        var match = Regex.Match(args, pattern);

        return match.Success
            ? match.Groups["value"].Value
            : null;
    }

    private static string? ExtractMethodBody(string content, string methodName)
    {
        var methodIndex = content.IndexOf(
            $"void {methodName}",
            StringComparison.OrdinalIgnoreCase);

        if (methodIndex < 0)
        {
            return null;
        }

        var openBraceIndex = content.IndexOf('{', methodIndex);

        if (openBraceIndex < 0)
        {
            return null;
        }

        var closeBraceIndex = FindMatchingBrace(content, openBraceIndex);

        if (closeBraceIndex < 0)
        {
            return null;
        }

        return content[(openBraceIndex + 1)..closeBraceIndex];
    }

    private static int FindMatchingBrace(string content, int openBraceIndex)
    {
        var depth = 0;

        for (var i = openBraceIndex; i < content.Length; i++)
        {
            if (content[i] == '{')
            {
                depth++;
            }
            else if (content[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    [GeneratedRegex(
        @"migrationBuilder\.(?<operation>[A-Za-z0-9_]+)\s*(?:<[^>]+>)?\s*\((?<args>.*?)\)\s*;",
        RegexOptions.Singleline)]
    private static partial Regex MigrationBuilderOperationRegex();
}