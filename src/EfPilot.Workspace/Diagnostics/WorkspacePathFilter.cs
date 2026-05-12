namespace EfPilot.Workspace.Diagnostics;

internal static class WorkspacePathFilter
{
    private static readonly string[] IgnoredDirectoryNames =
    [
        "bin",
        "obj",
        ".git",
        ".idea",
        ".vs",
        "node_modules"
    ];

    public static bool IsIgnored(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts.Any(part =>
            IgnoredDirectoryNames.Contains(part, StringComparer.OrdinalIgnoreCase));
    }
}
