namespace SharpSelecta.Tests;

public static class TestPaths
{
    private static readonly string Root = Path.GetPathRoot(Path.GetTempPath())!;

    public static string Rooted(params string[] segments) => Path.Combine([Root, .. segments]);

    public static string Music(params string[] segments) => Rooted(["music", .. segments]);

    public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
