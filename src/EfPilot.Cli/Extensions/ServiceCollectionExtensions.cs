using EfPilot.Cli.Commands;
using EfPilot.Cli.Profiles;
using EfPilot.Cli.Workflows;
using EfPilot.Core.Abstractions;
using EfPilot.Core.Migrations;
using EfPilot.EfCore.Execution;
using EfPilot.Workspace.Configuration;
using EfPilot.Workspace.Discovery;
using Microsoft.Extensions.DependencyInjection;

namespace EfPilot.Cli.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEfPilotCli(this IServiceCollection services)
    {
        services.AddSingleton<SolutionFinder>();
        services.AddSingleton<ProjectScanner>();
        services.AddSingleton<DbContextScanner>();
        services.AddSingleton<StartupProjectDetector>();
        services.AddSingleton<EfPilotConfigStore>();
        services.AddSingleton<CommandContextLoader>();
        services.AddSingleton<EfPilotCommandDispatcher>();
        services.AddSingleton<ProfileResolver>();
        services.AddSingleton<ProfileValidator>();
        services.AddTransient<AddMigrationWorkflow>();
        services.AddSingleton<MigrationFileAnalyzer>();
        services.AddSingleton<IMigrationCommandRunner, DotNetEfMigrationCommandRunner>();

        services.AddEfPilotCommand<InitCommand>(
            "init",
            "Initialize EfPilot configuration",
            "efpilot init");

        services.AddEfPilotCommand<AddCommand>(
            "add",
            "Create a migration",
            "efpilot add AddLoanCode --profile RateRiskDbContext [--verbose]");

        services.AddEfPilotCommand<RemoveCommand>(
            "remove",
            "Remove the last migration",
            "efpilot remove --profile RateRiskDbContext [--force] [--verbose]");

        services.AddEfPilotCommand<UpdateCommand>(
            "update",
            "Apply migrations to the database",
            "efpilot update --profile RateRiskDbContext [--to InitialCreate] [--verbose]");

        services.AddEfPilotCommand<StatusCommand>(
            "status",
            "Show applied and pending migrations",
            "efpilot status --profile RateRiskDbContext [--all] [--verbose]");

        services.AddEfPilotCommand<DiffCommand>(
            "diff",
            "Preview model changes",
            "efpilot diff --profile RateRiskDbContext [--verbose]");

        return services;
    }

    public static IServiceCollection AddEfPilotCommand<TCommand>(
        this IServiceCollection services,
        string name,
        string description,
        params string[] examples)
        where TCommand : EfPilotCommand
    {
        services.AddTransient<TCommand>();
        services.AddSingleton(EfPilotCommandDescriptor.Create<TCommand>(
            name,
            description,
            examples));

        return services;
    }
}
