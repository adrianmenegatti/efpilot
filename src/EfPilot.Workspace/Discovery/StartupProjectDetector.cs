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

        var candidates = projects
            .Where(project => !IsSameProject(project, dbContext.Project))
            .Where(LooksLikeExecutableProject)
            .Select(project => ScoreProject(project, dbContext))
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Project.Name)
            .ToList();

        var sameBoundedContextCandidates = candidates
            .Where(candidate => HasSameBoundedContext(candidate.Project, dbContext.Project))
            .ToList();

        return sameBoundedContextCandidates.Count > 0
            ? sameBoundedContextCandidates
            : candidates;
    }

    private static StartupProjectCandidate ScoreProject(
        WorkspaceProject project,
        DiscoveredDbContext dbContext)
    {
        var score = new CandidateScore();

        if (project.IsWebProject)
        {
            score.Add(50, "Web SDK project");
        }

        if (project.HasProgramCs)
        {
            score.Add(25, "Contains Program.cs");
        }

        if (project.HasAppSettings)
        {
            score.Add(15, "Contains appsettings.json");
        }

        if (LooksLikeApiProject(project))
        {
            score.Add(30, "Looks like API project");
        }

        if (LooksLikeWorkerOrHostProject(project))
        {
            score.Add(15, "Looks like worker/host project");
        }

        if (ReferencesProject(project, dbContext.Project))
        {
            score.Add(80, $"References {dbContext.Project.Name}");
        }

        if (HasSameBoundedContext(project, dbContext.Project))
        {
            score.Add(80, "Same bounded context");
        }

        var distanceScore = CalculateDirectoryProximityScore(project, dbContext.Project);

        if (distanceScore > 0)
        {
            score.Add(distanceScore, "Near DbContext project in directory tree");
        }

        return new StartupProjectCandidate
        {
            Project = project,
            Score = score.Total,
            Reasons = score.Reasons
        };
    }

    private static bool LooksLikeExecutableProject(WorkspaceProject project)
    {
        if (LooksLikeLibraryLayer(project))
        {
            return false;
        }

        return project.IsWebProject ||
               project.HasProgramCs ||
               LooksLikeApiProject(project) ||
               LooksLikeWorkerOrHostProject(project);
    }

    private static bool LooksLikeLibraryLayer(WorkspaceProject project)
    {
        return project.Name.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase) ||
               project.Name.EndsWith(".Application", StringComparison.OrdinalIgnoreCase) ||
               project.Name.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase) ||
               project.Name.EndsWith(".Persistence", StringComparison.OrdinalIgnoreCase) ||
               project.Name.EndsWith(".Data", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeApiProject(WorkspaceProject project)
    {
        return ContainsProjectToken(project.Name, "Api") ||
               ContainsProjectToken(project.Name, "Web");
    }

    private static bool LooksLikeWorkerOrHostProject(WorkspaceProject project)
    {
        return ContainsProjectToken(project.Name, "Worker") ||
               ContainsProjectToken(project.Name, "Host") ||
               ContainsProjectToken(project.Name, "Service");
    }

    private static bool ContainsProjectToken(string projectName, string token)
    {
        var parts = projectName
            .Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);

        return parts.Any(part =>
            string.Equals(part, token, StringComparison.OrdinalIgnoreCase));
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

    private static bool HasSameBoundedContext(
        WorkspaceProject project,
        WorkspaceProject dbContextProject)
    {
        var projectContext = InferBoundedContext(project);
        var dbContextProjectContext = InferBoundedContext(dbContextProject);

        return !string.IsNullOrWhiteSpace(projectContext) &&
               !string.IsNullOrWhiteSpace(dbContextProjectContext) &&
               string.Equals(projectContext, dbContextProjectContext, StringComparison.OrdinalIgnoreCase);
    }

    private static string? InferBoundedContext(WorkspaceProject project)
    {
        var parts = SplitPath(project.Directory);

        var appsIndex = IndexOf(parts, "apps");

        if (appsIndex >= 0 && appsIndex + 1 < parts.Count)
        {
            return parts[appsIndex + 1];
        }

        var srcIndex = IndexOf(parts, "src");

        if (srcIndex >= 0 && srcIndex + 1 < parts.Count)
        {
            var next = parts[srcIndex + 1];

            if (!LooksLikeTechnicalLayerName(next))
            {
                return next;
            }
        }

        return ExtractNamePrefix(project.Name);
    }

    private static string? ExtractNamePrefix(string projectName)
    {
        var parts = projectName
            .Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (parts.Count <= 1)
        {
            return null;
        }

        var first = parts[0];

        return LooksLikeTechnicalLayerName(first)
            ? null
            : first;
    }

    private static bool LooksLikeTechnicalLayerName(string value)
    {
        return value.Equals("api", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("web", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("worker", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("host", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("service", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("services", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("domain", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("application", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("infrastructure", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("persistence", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("data", StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateDirectoryProximityScore(
        WorkspaceProject project,
        WorkspaceProject dbContextProject)
    {
        var projectParts = SplitPath(project.Directory);
        var dbContextParts = SplitPath(dbContextProject.Directory);

        var commonPrefixLength = projectParts
            .Zip(dbContextParts)
            .TakeWhile(pair => string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase))
            .Count();

        if (commonPrefixLength == 0)
        {
            return 0;
        }

        var maxDepth = Math.Max(projectParts.Count, dbContextParts.Count);
        var proximity = (double)commonPrefixLength / maxDepth;

        return proximity switch
        {
            >= 0.8 => 30,
            >= 0.6 => 20,
            >= 0.4 => 10,
            _ => 0
        };
    }

    private static bool IsSameProject(
        WorkspaceProject project,
        WorkspaceProject otherProject)
    {
        return string.Equals(
            Path.GetFullPath(project.Path),
            Path.GetFullPath(otherProject.Path),
            StringComparison.OrdinalIgnoreCase);
    }

    private static int IndexOf(IReadOnlyList<string> parts, string value)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (string.Equals(parts[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static IReadOnlyList<string> SplitPath(string path)
    {
        return Path
            .GetFullPath(path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();
    }

    private sealed class CandidateScore
    {
        public int Total { get; private set; }

        public List<string> Reasons { get; } = [];

        public void Add(int points, string reason)
        {
            Total += points;
            Reasons.Add(reason);
        }
    }
}