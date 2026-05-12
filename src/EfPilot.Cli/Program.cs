using EfPilot.Cli.Commands;
using EfPilot.Cli.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddEfPilotCli();

var provider = services.BuildServiceProvider();
var dispatcher = provider.GetRequiredService<EfPilotCommandDispatcher>();

return await dispatcher.ExecuteAsync(args);
