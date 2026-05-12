using EfPilot.Cli.Commands;
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
        services.AddSingleton<MigrationFileAnalyzer>();
        services.AddSingleton<IMigrationCommandRunner, DotNetEfMigrationCommandRunner>();

        services.AddTransient<InitCommand>();
        services.AddTransient<AddCommand>();
        services.AddTransient<RemoveCommand>();
        services.AddTransient<UpdateCommand>();
        services.AddTransient<StatusCommand>();
        services.AddTransient<DiffCommand>();
        
        return services;
    }
}