using System.Text.RegularExpressions;
using EfPilot.Core.Diagnostics;
using EfPilot.Core.Workspace;

namespace EfPilot.Workspace.Diagnostics;

public sealed partial class DesignTimeFactoryAnalyzer
{
    public IReadOnlyList<DesignTimeFactoryInfo> Analyze(
        IReadOnlyList<WorkspaceProject> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        var factories = new List<DesignTimeFactoryInfo>();

        foreach (var project in projects)
        {
            var sourceFiles = Directory
                .EnumerateFiles(project.Directory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !WorkspacePathFilter.IsIgnored(path));

            foreach (var sourceFile in sourceFiles)
            {
                var content = File.ReadAllText(sourceFile);

                foreach (Match match in DesignTimeFactoryRegex().Matches(content))
                {
                    factories.Add(new DesignTimeFactoryInfo
                    {
                        FactoryName = match.Groups["factory"].Value,
                        DbContextName = match.Groups["context"].Value,
                        Project = project,
                        Path = sourceFile,
                        ReadsConfigurationFromCurrentDirectory =
                            ReadsConfigurationFromCurrentDirectory(content)
                    });
                }
            }
        }

        return factories
            .DistinctBy(factory => $"{factory.Project.Path}:{factory.FactoryName}:{factory.DbContextName}")
            .OrderBy(factory => factory.DbContextName)
            .ThenBy(factory => factory.Project.Name)
            .ToList();
    }

    private static bool ReadsConfigurationFromCurrentDirectory(string content)
    {
        return content.Contains("Directory.GetCurrentDirectory()", StringComparison.Ordinal) &&
               content.Contains("AddJsonFile(\"appsettings", StringComparison.Ordinal);
    }

    [GeneratedRegex(
        @"class\s+(?<factory>[A-Za-z_][A-Za-z0-9_]*)\s*(?:\([^)]*\))?\s*:\s*[A-Za-z0-9_<>,\s\.]*IDesignTimeDbContextFactory\s*<\s*(?<context>[A-Za-z_][A-Za-z0-9_]*)\s*>",
        RegexOptions.Multiline)]
    private static partial Regex DesignTimeFactoryRegex();
}
