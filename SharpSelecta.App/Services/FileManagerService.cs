using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Resources;

namespace SharpSelecta.App.Services;

public sealed class FileManagerService : IFileManagerService
{
    private readonly ILogger<FileManagerService> _logger;
    private readonly IFileManagerShell _shell;
    private readonly string? _linuxSelectCapableExecutable;

    public string ActionLabel { get; }

    public FileManagerService(ILogger<FileManagerService> logger, IFileManagerShell shell)
    {
        _logger = logger;
        _shell = shell;

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
            var (name, selectCapableExecutable) = shell.DetectLinuxFileManager();
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
                _shell.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
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
        if (await _shell.TryShowViaFileManager1Async(filePath))
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

    private void Start(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = true };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        _shell.Start(startInfo);
    }
}
