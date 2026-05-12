using System.Text.RegularExpressions;
using EfPilot.Core.Diagnostics;
using EfPilot.Core.Workspace;

namespace EfPilot.Workspace.Diagnostics;

public sealed partial class MigrationAnalyzer
{
    public IReadOnlyList<MigrationProjectInfo> Analyze(
        IReadOnlyList<WorkspaceProject> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        return projects
            .Select(AnalyzeProject)
            .Where(project => project.ModelSnapshots.Count > 0 ||
                              project.EmptyMigrations.Count > 0 ||
                              project.Project.Name.Contains("Migrations", StringComparison.OrdinalIgnoreCase))
            .OrderBy(project => project.Project.Name)
            .ToList();
    }

    private static MigrationProjectInfo AnalyzeProject(WorkspaceProject project)
    {
        var sourceFiles = Directory
            .EnumerateFiles(project.Directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !WorkspacePathFilter.IsIgnored(path))
            .ToList();

        var snapshots = sourceFiles
            .Where(path => Path.GetFileName(path).EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path)
            .ToList();

        var emptyMigrations = sourceFiles
            .Where(IsMigrationFile)
            .Where(path => EmptyUpMethodRegex().IsMatch(File.ReadAllText(path)))
            .OrderBy(path => path)
            .ToList();

        return new MigrationProjectInfo
        {
            Project = project,
            ModelSnapshots = snapshots,
            EmptyMigrations = emptyMigrations
        };
    }

    private static bool IsMigrationFile(string path)
    {
        var fileName = Path.GetFileName(path);

        return MigrationFileNameRegex().IsMatch(fileName) &&
               !fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) &&
               !fileName.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase) &&
               path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   .Any(part => part.Equals("Migrations", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"^\d{14}_.+\.cs$", RegexOptions.IgnoreCase)]
    private static partial Regex MigrationFileNameRegex();

    [GeneratedRegex(
        @"protected\s+override\s+void\s+Up\s*\(\s*MigrationBuilder\s+\w+\s*\)\s*\{\s*\}",
        RegexOptions.Singleline)]
    private static partial Regex EmptyUpMethodRegex();
}
