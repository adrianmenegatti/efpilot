using EfPilot.Core.Workspace;
using EfPilot.Workspace.Discovery;

namespace EfPilot.Workspace.Tests.Discovery;

public sealed class StartupProjectDetectorTests
{
    [Fact]
    public void DetectCandidates_ShouldPreferApiProjectInSameBoundedContext()
    {
        using var temp = TestDirectory.Create();

        var infrastructure = CreateProject(
            temp.Path,
            "apps/raterisk/infrastructure/RateRisk.Infrastructure",
            "RateRisk.Infrastructure",
            "Microsoft.NET.Sdk");

        var api = CreateProject(
            temp.Path,
            "apps/raterisk/api/RateRisk.Api",
            "RateRisk.Api",
            "Microsoft.NET.Sdk.Web");

        File.WriteAllText(Path.Combine(api.Directory, "Program.cs"), "");
        File.WriteAllText(Path.Combine(api.Directory, "appsettings.json"), "{}");

        var identityApi = CreateProject(
            temp.Path,
            "apps/identity/api/Identity.Api",
            "Identity.Api",
            "Microsoft.NET.Sdk.Web");

        File.WriteAllText(Path.Combine(identityApi.Directory, "Program.cs"), "");
        File.WriteAllText(Path.Combine(identityApi.Directory, "appsettings.json"), "{}");

        var dbContext = new DiscoveredDbContext
        {
            Name = "RateRiskDbContext",
            Project = infrastructure
        };

        var detector = new StartupProjectDetector();

        var result = detector.DetectCandidates(dbContext, [infrastructure, api, identityApi]);

        Assert.NotEmpty(result);
        Assert.Equal("RateRisk.Api", result[0].Project.Name);
        Assert.DoesNotContain(result, candidate => candidate.Project.Name == "Identity.Api");
    }

    [Fact]
    public void DetectCandidates_ShouldIncludeWorkerInSameBoundedContext()
    {
        using var temp = TestDirectory.Create();

        var infrastructure = CreateProject(
            temp.Path,
            "apps/raterisk/infrastructure/RateRisk.Infrastructure",
            "RateRisk.Infrastructure",
            "Microsoft.NET.Sdk");

        var worker = CreateProject(
            temp.Path,
            "apps/raterisk/worker/RateRisk.Worker",
            "RateRisk.Worker",
            "Microsoft.NET.Sdk");

        File.WriteAllText(Path.Combine(worker.Directory, "Program.cs"), "");
        File.WriteAllText(Path.Combine(worker.Directory, "appsettings.json"), "{}");

        var dbContext = new DiscoveredDbContext
        {
            Name = "RateRiskDbContext",
            Project = infrastructure
        };

        var detector = new StartupProjectDetector();

        var result = detector.DetectCandidates(dbContext, [infrastructure, worker]);

        Assert.Contains(result, candidate => candidate.Project.Name == "RateRisk.Worker");
    }

    private static WorkspaceProject CreateProject(
        string root,
        string relativeDirectory,
        string name,
        string sdk)
    {
        var directory = Path.Combine(root, relativeDirectory);
        Directory.CreateDirectory(directory);

        var projectPath = Path.Combine(directory, $"{name}.csproj");
        File.WriteAllText(projectPath, $"<Project Sdk=\"{sdk}\" />");

        return new WorkspaceProject
        {
            Name = name,
            Path = projectPath,
            Directory = directory,
            Sdk = sdk
        };
    }
}