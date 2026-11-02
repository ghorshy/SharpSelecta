namespace SharpSelecta.Tests;

public sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("sharpselecta-tests-").FullName;

    public string NewSettingsPath() => FilePath($"settings-{Guid.NewGuid():N}.json");

    public string FilePath(string name) => System.IO.Path.Combine(Path, name);

    public DirectoryInfo NewSubdirectory() => Directory.CreateDirectory(FilePath(Guid.NewGuid().ToString("N")));

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
