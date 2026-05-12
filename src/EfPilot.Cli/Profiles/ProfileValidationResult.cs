namespace EfPilot.Cli.Profiles;

public sealed record ProfileValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static ProfileValidationResult Valid { get; } = new([]);
}
