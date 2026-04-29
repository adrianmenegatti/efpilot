using System.Text.RegularExpressions;

namespace EfPilot.Core.Migrations;

public sealed class MigrationFileAnalyzer
{
    public bool IsEmptyMigration(string migrationFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationFilePath);

        var content = File.ReadAllText(migrationFilePath);

        return IsMethodBodyEmpty(content, "Up") &&
               IsMethodBodyEmpty(content, "Down");
    }

    private static bool IsMethodBodyEmpty(string content, string methodName)
    {
        var methodIndex = content.IndexOf(
            $"void {methodName}",
            StringComparison.OrdinalIgnoreCase);

        if (methodIndex < 0)
        {
            return false;
        }

        var openBraceIndex = content.IndexOf('{', methodIndex);

        if (openBraceIndex < 0)
        {
            return false;
        }

        var closeBraceIndex = FindMatchingBrace(content, openBraceIndex);

        if (closeBraceIndex < 0)
        {
            return false;
        }

        var body = content[(openBraceIndex + 1)..closeBraceIndex];

        return string.IsNullOrWhiteSpace(RemoveComments(body));
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

    private static string RemoveComments(string value)
    {
        var withoutLineComments = Regex.Replace(
            value,
            @"//.*?$",
            "",
            RegexOptions.Multiline);

        return Regex.Replace(
            withoutLineComments,
            @"/\*.*?\*/",
            "",
            RegexOptions.Singleline);
    }
}