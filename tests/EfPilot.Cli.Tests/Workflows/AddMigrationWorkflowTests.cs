using EfPilot.Cli.Commands;
using EfPilot.Cli.Profiles;
using EfPilot.Cli.Workflows;
using EfPilot.Core.Abstractions;
using EfPilot.Core.Configuration;
using EfPilot.Core.Migrations;
using EfPilot.Workspace.Configuration;
using EfPilot.Workspace.Discovery;

namespace EfPilot.Cli.Tests.Workflows;

public sealed class AddMigrationWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldRunAddMigration_WhenProfileIsValid()
    {
        using var directory = TestDirectory.Create();
        using var workingDirectory = TestWorkingDirectory.ChangeTo(directory.Path);

        var profile = new EfPilotProfile
        {
            Name = "App",
            DbContext = "AppDbContext",
            Project = "src/App.Data/App.Data.csproj",
            StartupProject = "src/App.Api/App.Api.csproj"
        };

        await WriteWorkspaceAsync(directory.Path, profile, createProjectFiles: true);

        var runner = new FakeMigrationCommandRunner
        {
            AddMigrationResult = MigrationCommandResult.Succeeded("created", "")
        };

        var workflow = CreateWorkflow(runner);
        var beforeRunCalled = false;

        var result = await workflow.ExecuteAsync(new AddMigrationWorkflowRequest(
            "AddCustomers",
            "App",
            selectedProfile =>
            {
                beforeRunCalled = true;
                AssertProfile(profile, selectedProfile);
            }));

        Assert.Equal(AddMigrationWorkflowStatus.Completed, result.Status);
        Assert.True(beforeRunCalled);
        Assert.NotNull(result.Profile);
        AssertProfile(profile, result.Profile);
        Assert.Same(runner.AddMigrationResult, result.MigrationResult);
        Assert.NotNull(runner.AddMigrationRequest);
        Assert.EndsWith(
            Path.GetFileName(directory.Path),
            runner.AddMigrationRequest.SolutionDirectory);
        AssertProfile(profile, runner.AddMigrationRequest.Profile);
        Assert.Equal("AddCustomers", runner.AddMigrationRequest.MigrationName);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnProfileNotFound_WhenNamedProfileDoesNotExist()
    {
        using var directory = TestDirectory.Create();
        using var workingDirectory = TestWorkingDirectory.ChangeTo(directory.Path);

        var profile = new EfPilotProfile
        {
            Name = "App",
            DbContext = "AppDbContext",
            Project = "src/App.Data/App.Data.csproj",
            StartupProject = "src/App.Api/App.Api.csproj"
        };

        await WriteWorkspaceAsync(directory.Path, profile, createProjectFiles: true);

        var runner = new FakeMigrationCommandRunner();
        var workflow = CreateWorkflow(runner);

        var result = await workflow.ExecuteAsync(new AddMigrationWorkflowRequest(
            "AddCustomers",
            "Missing"));

        Assert.Equal(AddMigrationWorkflowStatus.ProfileNotFound, result.Status);
        AssertProfile(profile, Assert.Single(result.AvailableProfiles));
        Assert.Null(result.MigrationResult);
        Assert.Null(runner.AddMigrationRequest);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnInvalidProfile_WhenProfilePathsDoNotExist()
    {
        using var directory = TestDirectory.Create();
        using var workingDirectory = TestWorkingDirectory.ChangeTo(directory.Path);

        var profile = new EfPilotProfile
        {
            Name = "App",
            DbContext = "AppDbContext",
            Project = "src/App.Data/App.Data.csproj",
            StartupProject = "src/App.Api/App.Api.csproj"
        };

        await WriteWorkspaceAsync(directory.Path, profile, createProjectFiles: false);

        var runner = new FakeMigrationCommandRunner();
        var workflow = CreateWorkflow(runner);

        var result = await workflow.ExecuteAsync(new AddMigrationWorkflowRequest(
            "AddCustomers",
            "App"));

        Assert.Equal(AddMigrationWorkflowStatus.InvalidProfile, result.Status);
        Assert.NotNull(result.Profile);
        AssertProfile(profile, result.Profile);
        Assert.NotNull(result.ValidationResult);
        Assert.False(result.ValidationResult.IsValid);
        Assert.Equal(2, result.ValidationResult.Errors.Count);
        Assert.Null(result.MigrationResult);
        Assert.Null(runner.AddMigrationRequest);
    }

    private static AddMigrationWorkflow CreateWorkflow(FakeMigrationCommandRunner runner)
    {
        return new AddMigrationWorkflow(
            new CommandContextLoader(
                new SolutionFinder(),
                new EfPilotConfigStore()),
            new ProfileResolver(),
            new ProfileValidator(),
            runner);
    }

    private static void AssertProfile(
        EfPilotProfile expected,
        EfPilotProfile actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.DbContext, actual.DbContext);
        Assert.Equal(expected.Project, actual.Project);
        Assert.Equal(expected.StartupProject, actual.StartupProject);
        Assert.Equal(expected.MigrationsFolder, actual.MigrationsFolder);
    }

    private static async Task WriteWorkspaceAsync(
        string solutionDirectory,
        EfPilotProfile profile,
        bool createProjectFiles)
    {
        var solutionPath = Path.Combine(solutionDirectory, "TestSolution.slnx");
        await File.WriteAllTextAsync(solutionPath, "<Solution />");

        if (createProjectFiles)
        {
            WriteProjectFile(solutionDirectory, profile.Project);
            WriteProjectFile(solutionDirectory, profile.StartupProject);
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
        public AddMigrationRequest? AddMigrationRequest { get; private set; }

        public MigrationCommandResult AddMigrationResult { get; init; } =
            MigrationCommandResult.Succeeded("", "");

        public Task<MigrationCommandResult> AddMigrationAsync(
            AddMigrationRequest request,
            CancellationToken cancellationToken = default)
        {
            AddMigrationRequest = request;

            return Task.FromResult(AddMigrationResult);
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
            throw new NotSupportedException();
        }
    }
}
