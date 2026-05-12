namespace EfPilot.Cli.Tests;

internal sealed class TestWorkingDirectory : IDisposable
{
    private readonly string _previousDirectory;

    private TestWorkingDirectory(string previousDirectory)
    {
        _previousDirectory = previousDirectory;
    }

    public static TestWorkingDirectory ChangeTo(string directory)
    {
        var previousDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(directory);

        return new TestWorkingDirectory(previousDirectory);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_previousDirectory);
    }
}
