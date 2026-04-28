namespace EfPilot.Core.Migrations;

public sealed record MigrationOperationSummary(
    string Operation,
    string Description);