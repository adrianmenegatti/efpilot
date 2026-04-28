using EfPilot.Workspace.Discovery;

namespace EfPilot.Workspace.Tests.Discovery;

public sealed class SolutionFinderTests
{
    [Fact]
    public void FindSolutionFile_ShouldFindSlnFile_FromCurrentDirectory()
    {
        using var temp = TestDirectory.Create();

        var solutionPath = Path.Combine(temp.Path, "TestSolution.sln");
        File.WriteAllText(solutionPath, "");

        var finder = new SolutionFinder();

        var result = finder.FindSolutionFile(temp.Path);

        Assert.Equal(solutionPath, result);
    }

    [Fact]
    public void FindSolutionFile_ShouldFindSlnxFile()
    {
        using var temp = TestDirectory.Create();

        var solutionPath = Path.Combine(temp.Path, "TestSolution.slnx");
        File.WriteAllText(solutionPath, "");

        var finder = new SolutionFinder();

        var result = finder.FindSolutionFile(temp.Path);

        Assert.Equal(solutionPath, result);
    }

    [Fact]
    public void FindSolutionFile_ShouldFindSolutionFromChildDirectory()
    {
        using var temp = TestDirectory.Create();

        var solutionPath = Path.Combine(temp.Path, "TestSolution.slnx");
        File.WriteAllText(solutionPath, "");

        var child = Path.Combine(temp.Path, "src", "App");
        Directory.CreateDirectory(child);

        var finder = new SolutionFinder();

        var result = finder.FindSolutionFile(child);

        Assert.Equal(solutionPath, result);
    }

    [Fact]
    public void FindSolutionFile_ShouldThrow_WhenMultipleSolutionsExistInSameDirectory()
    {
        using var temp = TestDirectory.Create();

        File.WriteAllText(Path.Combine(temp.Path, "A.sln"), "");
        File.WriteAllText(Path.Combine(temp.Path, "B.slnx"), "");

        var finder = new SolutionFinder();

        Assert.Throws<InvalidOperationException>(() =>
            finder.FindSolutionFile(temp.Path));
    }
}