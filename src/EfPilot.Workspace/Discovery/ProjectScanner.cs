using System.Xml.Linq;
using EfPilot.Core.Workspace;

namespace EfPilot.Workspace.Discovery;

public class ProjectScanner
{
private static readonly string[] IgnoredDirectoryNames =
    [
        "bin",
        "obj",
        ".git",
        ".idea",
        ".vs",
        "node_modules"
    ];

    public IReadOnlyList<WorkspaceProject> ScanProjects(string solutionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionDirectory);

        var projectFiles = Directory
            .EnumerateFiles(solutionDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsInIgnoredDirectory(path))
            .OrderBy(path => path)
            .ToList();

        return projectFiles
            .Select(ReadProject)
            .ToList();
    }

    private static WorkspaceProject ReadProject(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var root = document.Root;

        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var sdk = root?.Attribute("Sdk")?.Value;

        var references = document
            .Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeProjectReference(projectDirectory, value!))
            .ToList();

        return new WorkspaceProject
        {
            Name = projectName,
            Path = projectPath,
            Directory = projectDirectory,
            Sdk = sdk,
            ProjectReferences = references
        };
    }

    private static string NormalizeProjectReference(string projectDirectory, string includeValue)
    {
        var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, includeValue));
        return fullPath;
    }

    private static bool IsInIgnoredDirectory(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts.Any(part =>
            IgnoredDirectoryNames.Contains(part, StringComparer.OrdinalIgnoreCase));
    }}