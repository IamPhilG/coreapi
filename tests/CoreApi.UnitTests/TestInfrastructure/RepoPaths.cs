namespace CoreApi.UnitTests.TestInfrastructure;

/// <summary>Locates the repository root from the test assembly so tests can read committed files
/// (CI workflow, evidence script) regardless of the working directory.</summary>
public static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Combine(params string[] segments) =>
        Path.Combine(new[] { Root }.Concat(segments).ToArray());

    private static string FindRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "coreapi.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (coreapi.sln).");
    }
}
