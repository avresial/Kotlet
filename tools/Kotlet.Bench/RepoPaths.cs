namespace Kotlet.Bench;

/// <summary>
/// Locates repository files from the build output. The benchmark runs from
/// <c>bin/Debug/...</c>, so paths are resolved by walking up to the solution file rather than
/// by counting directory levels.
/// </summary>
public static class RepoPaths
{
    private const string SolutionFile = "Kotlet.slnx";

    public static string Root { get; } = FindRoot();

    public static string ApiProject => Path.Combine(Root, "src", "backend", "Kotlet.Api");

    public static string BenchProject => Path.Combine(Root, "tools", "Kotlet.Bench");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find {SolutionFile} above {AppContext.BaseDirectory}. " +
            "Run the benchmark from inside the repository.");
    }
}
