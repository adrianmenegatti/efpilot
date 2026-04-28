namespace EfPilot.Core.Workspace;

public class WorkspaceProject
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public required string Directory { get; init; }

    public string? Sdk { get; init; }

    public List<string> ProjectReferences { get; init; } = [];

    public bool IsWebProject =>
        string.Equals(Sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase);

    public bool HasProgramCs =>
        File.Exists(System.IO.Path.Combine(Directory, "Program.cs"));

    public bool HasAppSettings =>
        File.Exists(System.IO.Path.Combine(Directory, "appsettings.json"));
}