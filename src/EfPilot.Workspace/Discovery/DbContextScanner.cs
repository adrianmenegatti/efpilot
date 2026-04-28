using System.Text.RegularExpressions;
using EfPilot.Core.Workspace;

namespace EfPilot.Workspace.Discovery;

public sealed partial class DbContextScanner
{
    public IReadOnlyList<DiscoveredDbContext> ScanDbContexts(
        IReadOnlyList<WorkspaceProject> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        var result = (from project in projects
            let sourceFiles = Directory.EnumerateFiles(project.Directory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsGeneratedOrBuildArtifact(path))
            from sourceFile in sourceFiles
            let content = File.ReadAllText(sourceFile)
            from Match match in DbContextClassRegex().Matches(content)
            let dbContextName = match.Groups["name"].Value
            let baseType = match.Groups["baseType"].Value.Trim()
            where !IsFactoryOrDesignTimeHelper(dbContextName)
            where LooksLikeDbContextBaseType(baseType)
            select new DiscoveredDbContext { Name = dbContextName, Project = project }).ToList();

        return result
            .DistinctBy(x => $"{x.Project.Path}:{x.Name}")
            .OrderBy(x => x.Project.Path)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static bool IsGeneratedOrBuildArtifact(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts.Any(part =>
            string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFactoryOrDesignTimeHelper(string className)
    {
        return className.EndsWith("Factory", StringComparison.OrdinalIgnoreCase) ||
               className.EndsWith("DbContextFactory", StringComparison.OrdinalIgnoreCase) ||
               className.Contains("DesignTime", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeDbContextBaseType(string baseType)
    {
        return baseType.Equals("DbContext", StringComparison.OrdinalIgnoreCase) ||
               baseType.EndsWith(".DbContext", StringComparison.OrdinalIgnoreCase) ||
               baseType.Contains("IdentityDbContext", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(
        @"class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:\([^)]*\))?\s*:\s*(?<baseType>[A-Za-z0-9_<>,\s\.]*DbContext(?:<[^>]+>)?)(?:\([^)]*\))?",
        RegexOptions.Multiline)]
    private static partial Regex DbContextClassRegex();
}