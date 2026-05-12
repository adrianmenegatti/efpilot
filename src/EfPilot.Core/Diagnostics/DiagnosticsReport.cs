using EfPilot.Core.Workspace;

namespace EfPilot.Core.Diagnostics;

public sealed class DiagnosticsReport
{
    public required string SolutionDirectory { get; init; }

    public required IReadOnlyList<WorkspaceProject> Projects { get; init; }

    public required IReadOnlyList<DiscoveredDbContext> DbContexts { get; init; }

    public required IReadOnlyList<DesignTimeFactoryInfo> DesignTimeFactories { get; init; }

    public required IReadOnlyList<StartupProjectInfo> StartupProjects { get; init; }

    public required IReadOnlyList<MigrationProjectInfo> MigrationProjects { get; init; }

    public required IReadOnlyList<DiagnosticsFinding> Findings { get; init; }

    public bool HasErrors => Findings.Any(finding => finding.Severity == DiagnosticsSeverity.Error);

    public bool HasWarnings => Findings.Any(finding => finding.Severity == DiagnosticsSeverity.Warning);
}
