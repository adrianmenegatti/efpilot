using EfPilot.Cli.Commands;
using EfPilot.Cli.Profiles;
using EfPilot.Core.Abstractions;
using EfPilot.Core.Configuration;
using EfPilot.Core.Migrations;

namespace EfPilot.Cli.Workflows;

public sealed class AddMigrationWorkflow(
    CommandContextLoader contextLoader,
    ProfileResolver profileResolver,
    ProfileValidator profileValidator,
    IMigrationCommandRunner runner)
{
    public async Task<AddMigrationWorkflowResult> ExecuteAsync(
        AddMigrationWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = await contextLoader.LoadAsync(cancellationToken);

        if (context is null)
        {
            return AddMigrationWorkflowResult.ContextUnavailable();
        }

        var profile = profileResolver.Resolve(
            context.Config.Profiles,
            request.ProfileName);

        if (profile is null)
        {
            return AddMigrationWorkflowResult.ProfileNotFound(context.Config.Profiles);
        }

        var validation = profileValidator.ValidatePaths(
            context.SolutionDirectory,
            profile);

        if (!validation.IsValid)
        {
            return AddMigrationWorkflowResult.InvalidProfile(profile, validation);
        }

        request.BeforeRun?.Invoke(profile);

        var result = await runner.AddMigrationAsync(new AddMigrationRequest
        {
            SolutionDirectory = context.SolutionDirectory,
            Profile = profile,
            MigrationName = request.MigrationName
        }, cancellationToken);

        return AddMigrationWorkflowResult.Completed(profile, result);
    }
}

public sealed record AddMigrationWorkflowRequest(
    string MigrationName,
    string? ProfileName,
    Action<EfPilotProfile>? BeforeRun = null);

public sealed record AddMigrationWorkflowResult(
    AddMigrationWorkflowStatus Status,
    EfPilotProfile? Profile,
    MigrationCommandResult? MigrationResult,
    IReadOnlyList<EfPilotProfile> AvailableProfiles,
    ProfileValidationResult? ValidationResult)
{
    public static AddMigrationWorkflowResult ContextUnavailable()
    {
        return new AddMigrationWorkflowResult(
            AddMigrationWorkflowStatus.ContextUnavailable,
            Profile: null,
            MigrationResult: null,
            AvailableProfiles: [],
            ValidationResult: null);
    }

    public static AddMigrationWorkflowResult ProfileNotFound(
        IReadOnlyList<EfPilotProfile> availableProfiles)
    {
        return new AddMigrationWorkflowResult(
            AddMigrationWorkflowStatus.ProfileNotFound,
            Profile: null,
            MigrationResult: null,
            AvailableProfiles: availableProfiles,
            ValidationResult: null);
    }

    public static AddMigrationWorkflowResult InvalidProfile(
        EfPilotProfile profile,
        ProfileValidationResult validationResult)
    {
        return new AddMigrationWorkflowResult(
            AddMigrationWorkflowStatus.InvalidProfile,
            profile,
            MigrationResult: null,
            AvailableProfiles: [],
            validationResult);
    }

    public static AddMigrationWorkflowResult Completed(
        EfPilotProfile profile,
        MigrationCommandResult result)
    {
        return new AddMigrationWorkflowResult(
            AddMigrationWorkflowStatus.Completed,
            profile,
            result,
            AvailableProfiles: [],
            ValidationResult: null);
    }
}

public enum AddMigrationWorkflowStatus
{
    ContextUnavailable,
    ProfileNotFound,
    InvalidProfile,
    Completed
}
