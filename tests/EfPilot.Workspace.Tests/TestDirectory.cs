namespace EfPilot.Workspace.Tests;

internal sealed class TestDirectory : IDisposable
{
    public string Path { get; }

    private TestDirectory(string path)
    {
        Path = path;
    }

    public static TestDirectory Create()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "efpilot-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);

        return new TestDirectory(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}