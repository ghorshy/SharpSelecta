namespace SharpSelecta.App;

// %LocalAppData%\SharpSelecta on Windows, ~/.local/share/SharpSelecta on Linux.
public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SharpSelecta");

    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");
}
