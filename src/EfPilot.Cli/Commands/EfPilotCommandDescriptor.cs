namespace EfPilot.Cli.Commands;

public sealed record EfPilotCommandDescriptor(
    string Name,
    string Description,
    Type CommandType,
    IReadOnlyList<string> Examples)
{
    public static EfPilotCommandDescriptor Create<TCommand>(
        string name,
        string description,
        params string[] examples)
        where TCommand : EfPilotCommand
    {
        return new EfPilotCommandDescriptor(
            name,
            description,
            typeof(TCommand),
            examples);
    }
}
