using System.ComponentModel;
using System.Diagnostics;
using Tmds.DBus;

namespace SharpSelecta.App.Services;

public sealed class SystemFileManagerShell : IFileManagerShell
{
    private static readonly ObjectPath FileManager1Path = new("/org/freedesktop/FileManager1");

    public async Task<bool> TryShowViaFileManager1Async(string filePath)
    {
        if (Address.Session is not { } sessionAddress)
            return false;

        try
        {
            using var connection = new Connection(sessionAddress);
            await connection.ConnectAsync();

            var fileManager = connection.CreateProxy<IFileManager1>("org.freedesktop.FileManager1", FileManager1Path);
            await fileManager.ShowItemsAsync([new Uri(filePath).AbsoluteUri], string.Empty);
            return true;
        }
        catch (Exception ex) when (ex is DBusException or ConnectException)
        {
            return false;
        }
    }

    public void Start(ProcessStartInfo startInfo) => Process.Start(startInfo);

    public (string? Name, string? SelectCapableExecutable) DetectLinuxFileManager()
    {
        try
        {
            var desktopFileId = RunAndReadStdout("xdg-mime", "query", "default", "inode/directory")?.Trim();
            if (string.IsNullOrEmpty(desktopFileId))
                return (null, null);

            var selectCapableExecutable = LinuxFileManagerRecognition.SelectCapableExecutable(desktopFileId);

            var desktopFilePath = FindDesktopFile(desktopFileId);
            var name = desktopFilePath is null ? null : DesktopEntryName.ExtractName(File.ReadAllText(desktopFilePath));

            return (name, selectCapableExecutable);
        }
        catch (Exception ex) when (ex is IOException or Win32Exception or UnauthorizedAccessException)
        {
            return (null, null);
        }
    }

    private static string? RunAndReadStdout(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
            return null;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(2000);
        return process.ExitCode == 0 ? output : null;
    }

    private static string? FindDesktopFile(string desktopFileId)
    {
        foreach (var directory in ApplicationDirectories())
        {
            var candidate = Path.Combine(directory, desktopFileId);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> ApplicationDirectories()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        yield return Path.Combine(
            string.IsNullOrEmpty(dataHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
                : dataHome,
            "applications");

        var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        var directories = string.IsNullOrEmpty(dataDirs) ? "/usr/local/share:/usr/share" : dataDirs;
        foreach (var directory in directories.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return Path.Combine(directory, "applications");
        }
    }
}
