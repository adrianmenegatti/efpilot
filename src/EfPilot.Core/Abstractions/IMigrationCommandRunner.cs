using EfPilot.Core.Migrations;

namespace EfPilot.Core.Abstractions;

public interface IMigrationCommandRunner
{
    Task<MigrationCommandResult> AddMigrationAsync(
        AddMigrationRequest request,
        CancellationToken cancellationToken = default);

    Task<MigrationCommandResult> RemoveMigrationAsync(
        RemoveMigrationRequest request,
        CancellationToken cancellationToken = default);

    Task<MigrationCommandResult> UpdateDatabaseAsync(
        UpdateDatabaseRequest request,
        CancellationToken cancellationToken = default);
}