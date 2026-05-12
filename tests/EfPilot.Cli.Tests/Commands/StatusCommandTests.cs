using EfPilot.Cli.Commands;
using EfPilot.Cli.Profiles;
using EfPilot.Core.Abstractions;
using EfPilot.Core.Configuration;
using EfPilot.Core.Migrations;
using EfPilot.Workspace.Configuration;
using EfPilot.Workspace.Diagnostics;
using EfPilot.Workspace.Discovery;

namespace EfPilot.Cli.Tests.Commands;

public sealed class StatusCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldTryWithoutStartupProject_WhenDesignTimeFactoryExists()
    {
        using var directory = TestDirectory.Create();
        using var workingDirectory = TestWorkingDirectory.ChangeTo(directory.Path);

        var profile = CreateProfile();
        await WriteWorkspaceAsync(directory.Path, profile, createFactory: true);

        var runner = new FakeMigrationCommandRunner();
        var command = CreateCommand(runner);

        var exitCode = await command.ExecuteAsync(["--profile", profile.Name]);

        Assert.Equal(0, exitCode);
        var request = Assert.Single(runner.StatusRequests);
        Assert.False(request.UseStartupProject);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFallbackToStartupProject_WhenDesignTimeFactoryPathFails()
    {
        using var directory = TestDirectory.Create();
        using var workingDirectory = TestWorkingDirectory.ChangeTo(directory.Path);

        var profile = CreateProfile();
        await WriteWorkspaceAsync(directory.Path, profile, createFactory: true);

        var runner = new FakeMigrationCommandRunner
        {
            FailWithoutStartupProject = true
        };

        var command = CreateCommand(runner);

        var exitCode = await command.ExecuteAsync(["--profile", profile.Name]);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, runner.StatusRequests.Count);
        Assert.False(runner.StatusRequests[0].UseStartupProject);
        Assert.True(runner.StatusRequests[1].UseStartupProject);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseStartupProject_WhenNoDesignTimeFactoryExists()
    {
        using var directory = TestDirectory.Create();
        using var workingDirectory = TestWorkingDirectory.ChangeTo(directory.Path);

        var profile = CreateProfile();
        await WriteWorkspaceAsync(directory.Path, profile, createFactory: false);

        var runner = new FakeMigrationCommandRunner();
        var command = CreateCommand(runner);

        var exitCode = await command.ExecuteAsync(["--profile", profile.Name]);

        Assert.Equal(0, exitCode);
        var request = Assert.Single(runner.StatusRequests);
        Assert.True(request.UseStartupProject);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseStartupProject_WhenFactoryReadsAppSettingsFromCurrentDirectory()
    {
        using var directory = TestDirectory.Create();
        using var workingDirectory = TestWorkingDirectory.ChangeTo(directory.Path);

        var profile = CreateProfile();
        await WriteWorkspaceAsync(
            directory.Path,
            profile,
            createFactory: true,
            factoryReadsCurrentDirectoryConfiguration: true);

        var runner = new FakeMigrationCommandRunner();
        var command = CreateCommand(runner);

        var exitCode = await command.ExecuteAsync(["--profile", profile.Name]);

        Assert.Equal(0, exitCode);
        var request = Assert.Single(runner.StatusRequests);
        Assert.True(request.UseStartupProject);
    }

    private static StatusCommand CreateCommand(FakeMigrationCommandRunner runner)
    {
        return new StatusCommand(
            runner,
            new CommandContextLoader(
                new SolutionFinder(),
                new EfPilotConfigStore()),
            new ProfileResolver(),
            new ProfileValidator(),
            new ProjectScanner(),
            new DesignTimeFactoryAnalyzer());
    }

    private static EfPilotProfile CreateProfile()
    {
        return new EfPilotProfile
        {
            Name = "RateRisk",
            DbContext = "RateRiskDbContext",
            Project = "apps/raterisk/infrastructure/RateRisk.Infrastructure/RateRisk.Infrastructure.csproj",
            StartupProject = "apps/raterisk/api/RateRisk.Api/RateRisk.Api.csproj"
        };
    }

    private static async Task WriteWorkspaceAsync(
        string solutionDirectory,
        EfPilotProfile profile,
        bool createFactory,
        bool factoryReadsCurrentDirectoryConfiguration = false)
    {
        await File.WriteAllTextAsync(
            Path.Combine(solutionDirectory, "TestSolution.slnx"),
            "<Solution />");

        WriteProjectFile(solutionDirectory, profile.Project);
        WriteProjectFile(solutionDirectory, profile.StartupProject);

        if (createFactory)
        {
            var projectDirectory = Path.GetDirectoryName(
                Path.Combine(solutionDirectory, profile.Project))!;

            await File.WriteAllTextAsync(
                Path.Combine(projectDirectory, "RateRiskDbContextFactory.cs"),
                factoryReadsCurrentDirectoryConfiguration
                    ? """
                      using Microsoft.EntityFrameworkCore.Design;
                      using Microsoft.Extensions.Configuration;

                      public sealed class RateRiskDbContextFactory : IDesignTimeDbContextFactory<RateRiskDbContext>
                      {
                          public RateRiskDbContext CreateDbContext(string[] args)
                          {
                              var configuration = new ConfigurationBuilder()
                                  .SetBasePath(Directory.GetCurrentDirectory())
                                  .AddJsonFile("appsettings.json", optional: true)
                                  .Build();

                              return null!;
                          }
                      }
                      """
                    : """
                      using Microsoft.EntityFrameworkCore.Design;

                      public sealed class RateRiskDbContextFactory : IDesignTimeDbContextFactory<RateRiskDbContext>
                      {
                      }
                      """);
        }

        var store = new EfPilotConfigStore();
        await store.SaveAsync(solutionDirectory, new EfPilotConfig
        {
            Solution = "TestSolution.slnx",
            Profiles = [profile]
        });
    }

    private static void WriteProjectFile(string solutionDirectory, string relativePath)
    {
        var fullPath = Path.Combine(solutionDirectory, relativePath);
        var projectDirectory = Path.GetDirectoryName(fullPath)!;

        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(fullPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
    }

    private sealed class FakeMigrationCommandRunner : IMigrationCommandRunner
    {
        public List<MigrationStatusRequest> StatusRequests { get; } = [];

        public bool FailWithoutStartupProject { get; init; }

        public Task<MigrationCommandResult> AddMigrationAsync(
            AddMigrationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<MigrationCommandResult> RemoveMigrationAsync(
            RemoveMigrationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<MigrationCommandResult> UpdateDatabaseAsync(
            UpdateDatabaseRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<MigrationCommandResult> GetStatusAsync(
            MigrationStatusRequest request,
            CancellationToken cancellationToken = default)
        {
            StatusRequests.Add(request);

            if (FailWithoutStartupProject && !request.UseStartupProject)
            {
                return Task.FromResult(MigrationCommandResult.Failed(
                    "",
                    "Could not create DbContext.",
                    1));
            }

            return Task.FromResult(MigrationCommandResult.Succeeded(
                "20260512000100_InitialCreate",
                ""));
        }
    }
}
