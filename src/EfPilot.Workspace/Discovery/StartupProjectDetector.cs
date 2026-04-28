using EfPilot.Core.Workspace;

namespace EfPilot.Workspace.Discovery;

public sealed class StartupProjectDetector
{
    public IReadOnlyList<StartupProjectCandidate> DetectCandidates(
        DiscoveredDbContext dbContext,
        IReadOnlyList<WorkspaceProject> projects)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(projects);

        return projects
            .Where(LooksLikeExecutableProject)
            .Select(project => ScoreProject(project, dbContext))
            .Where(candidate => candidate.Score > 0)
            .Where(candidate => IsSameContextOrNoBetterOption(candidate, dbContext))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Project.Name)
            .ToList();
    }

    private static StartupProjectCandidate ScoreProject(
        WorkspaceProject project,
        DiscoveredDbContext dbContext)
    {
        var score = 0;
        var reasons = new List<string>();

        if (project.IsWebProject)
        {
            score += 40;
            reasons.Add("Web SDK project");
        }

        if (project.HasProgramCs)
        {
            score += 20;
            reasons.Add("Contains Program.cs");
        }

        if (project.HasAppSettings)
        {
            score += 10;
            reasons.Add("Contains appsettings.json");
        }

        if (project.Name.Contains("Api", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
            reasons.Add("Project name contains Api");
        }

        if (project.Name.Contains("Web", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            reasons.Add("Project name contains Web");
        }

        if (ReferencesProject(project, dbContext.Project))
        {
            score += 60;
            reasons.Add($"References {dbContext.Project.Name}");
        }

        if (IsNearInDirectoryTree(project, dbContext.Project))
        {
            score += 15;
            reasons.Add("Near DbContext project in directory tree");
        }
        
        if (LooksLikeSameBoundedContext(project, dbContext.Project))
        {
            score += 80;
            reasons.Add("Same bounded context");
        }

        return new StartupProjectCandidate
        {
            Project = project,
            Score = score,
            Reasons = reasons
        };
    }

    private static bool ReferencesProject(
        WorkspaceProject project,
        WorkspaceProject targetProject)
    {
        return project.ProjectReferences.Any(reference =>
            string.Equals(
                Path.GetFullPath(reference),
                Path.GetFullPath(targetProject.Path),
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNearInDirectoryTree(
        WorkspaceProject project,
        WorkspaceProject dbContextProject)
    {
        var projectParts = SplitPath(project.Directory);
        var dbContextParts = SplitPath(dbContextProject.Directory);

        var common = projectParts
            .Zip(dbContextParts)
            .TakeWhile(pair => string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase))
            .Count();

        return common >= Math.Min(projectParts.Count, dbContextParts.Count) - 2;
    }

    private static IReadOnlyList<string> SplitPath(string path)
    {
        return Path
            .GetFullPath(path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();
    }
    
    private static bool LooksLikeSameBoundedContext(
        WorkspaceProject project,
        WorkspaceProject dbContextProject)
    {
        var projectParts = SplitPath(project.Directory);
        var dbContextParts = SplitPath(dbContextProject.Directory);

        return projectParts.Intersect(
                dbContextParts,
                StringComparer.OrdinalIgnoreCase)
            .Any(part =>
                part.Equals("identity", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("raterisk", StringComparison.OrdinalIgnoreCase));
    }
    
    private static bool LooksLikeExecutableProject(WorkspaceProject project)
    {
        return project.IsWebProject ||
               project.HasProgramCs ||
               project.Name.Contains("Api", StringComparison.OrdinalIgnoreCase) ||
               project.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase) ||
               project.Name.Contains("Host", StringComparison.OrdinalIgnoreCase);
    }
    
    private static bool IsSameContextOrNoBetterOption(
        StartupProjectCandidate candidate,
        DiscoveredDbContext dbContext)
    {
        return LooksLikeSameBoundedContext(candidate.Project, dbContext.Project) ||
               ReferencesProject(candidate.Project, dbContext.Project);
    }
}