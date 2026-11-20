namespace SharpSelecta.App;

// %AppData%\SharpSelecta on Windows, ~/.local/share/SharpSelecta on Linux. Not %LocalAppData% on Windows:
// the installer puts the app itself in %LocalAppData%\SharpSelecta and deletes that folder on uninstall.
public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(OperatingSystem.IsWindows()
            ? Environment.SpecialFolder.ApplicationData
            : Environment.SpecialFolder.LocalApplicationData),
        "SharpSelecta");

    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");
}
