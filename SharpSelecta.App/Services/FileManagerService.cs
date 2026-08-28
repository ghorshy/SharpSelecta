using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Resources;

namespace SharpSelecta.App.Services;

public sealed class FileManagerService : IFileManagerService
{
    private readonly ILogger<FileManagerService> _logger;

    public string ActionLabel { get; }

    public FileManagerService(ILogger<FileManagerService> logger)
    {
        _logger = logger;
        ActionLabel = Strings.ShowInFileManager(DetectName());
    }

    public void RevealInFileManager(string filePath)
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

            // No universal "select this file" across Linux file managers - open its folder instead.
            // Selecting the file too is a possible future improvement (per-file-manager flags).
            var folder = Path.GetDirectoryName(filePath);
            if (folder is not null)
            {
                Start("xdg-open", folder);
            }
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open the file manager for {FilePath}", filePath);
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

    private static string DetectName()
    {
        if (OperatingSystem.IsWindows())
            return "Explorer";

        if (OperatingSystem.IsMacOS())
            return "Finder";

        return DetectLinuxFileManagerName() ?? Strings.GenericFileManager;
    }

    private static string? DetectLinuxFileManagerName()
    {
        try
        {
            var desktopFileId = RunAndReadStdout("xdg-mime", "query", "default", "inode/directory")?.Trim();
            if (string.IsNullOrEmpty(desktopFileId))
                return null;

            var desktopFilePath = FindDesktopFile(desktopFileId);
            return desktopFilePath is null ? null : DesktopEntryName.ExtractName(File.ReadAllText(desktopFilePath));
        }
        catch (Exception ex) when (ex is IOException or Win32Exception or UnauthorizedAccessException)
        {
            return null;
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
