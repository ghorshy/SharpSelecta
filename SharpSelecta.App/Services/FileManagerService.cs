using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Resources;
using Tmds.DBus;

namespace SharpSelecta.App.Services;

public sealed class FileManagerService : IFileManagerService
{
    private static readonly ObjectPath FileManager1Path = new("/org/freedesktop/FileManager1");

    private readonly ILogger<FileManagerService> _logger;
    private readonly string? _linuxSelectCapableExecutable;

    public string ActionLabel { get; }

    public FileManagerService(ILogger<FileManagerService> logger)
    {
        _logger = logger;

        if (OperatingSystem.IsWindows())
        {
            ActionLabel = Strings.ShowInFileManager("Explorer");
        }
        else if (OperatingSystem.IsMacOS())
        {
            ActionLabel = Strings.ShowInFileManager("Finder");
        }
        else
        {
            var (name, selectCapableExecutable) = DetectLinuxFileManager();
            _linuxSelectCapableExecutable = selectCapableExecutable;
            ActionLabel = Strings.ShowInFileManager(name ?? Strings.GenericFileManager);
        }
    }

    public async Task RevealInFileManagerAsync(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // /select, and the quoted path must travel as one argument - explorer.exe doesn't
                // parse it correctly split across ArgumentList entries.
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                Start("open", "-R", filePath);
                return;
            }

            await RevealOnLinuxAsync(filePath);
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open the file manager for {FilePath}", filePath);
        }
    }

    private async Task RevealOnLinuxAsync(string filePath)
    {
        // Preferred path: the standard freedesktop D-Bus interface. It's what actually selects the
        // file in Thunar and PCManFM-Qt too (their CLIs don't support it), talking to whatever
        // instance is already running rather than launching a new one.
        if (await TryShowViaFileManager1Async(filePath))
            return;

        if (_linuxSelectCapableExecutable is { } executable)
        {
            Start(executable, "--select", filePath);
            return;
        }

        var folder = Path.GetDirectoryName(filePath);
        if (folder is not null)
        {
            Start("xdg-open", folder);
        }
    }

    private static async Task<bool> TryShowViaFileManager1Async(string filePath)
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

    private static void Start(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = true };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process.Start(startInfo);
    }

    private static (string? Name, string? SelectCapableExecutable) DetectLinuxFileManager()
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
