namespace EfPilot.Workspace.Discovery;

public sealed class SolutionFinder
{
    private static readonly string[] SolutionExtensions = [".sln", ".slnx"];

    public string? FindSolutionFile(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            var solutionFiles = directory
                .GetFiles("*.*", SearchOption.TopDirectoryOnly)
                .Where(f => SolutionExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
                .ToList();

            switch (solutionFiles.Count)
            {
                case 1:
                    return solutionFiles[0].FullName;
                case > 1:
                {
                    var names = string.Join(", ", solutionFiles.Select(f => f.Name));

                    throw new InvalidOperationException(
                        $"Multiple solution files found in '{directory.FullName}': {names}. Please run efpilot from a more specific directory.");
                }
                default:
                    directory = directory.Parent;
                    break;
            }
        }

        return null;
    }
}