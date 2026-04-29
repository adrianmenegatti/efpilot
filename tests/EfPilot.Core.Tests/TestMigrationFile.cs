namespace EfPilot.Core.Tests;

internal sealed class TestMigrationFile : IDisposable
{
    public string Path { get; }

    private TestMigrationFile(string path)
    {
        Path = path;
    }

    public static TestMigrationFile Create(string content)
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "efpilot-core-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        var path = System.IO.Path.Combine(directory, "TestMigration.cs");
        File.WriteAllText(path, content);

        return new TestMigrationFile(path);
    }

    public void Dispose()
    {
        var directory = System.IO.Path.GetDirectoryName(Path);

        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}