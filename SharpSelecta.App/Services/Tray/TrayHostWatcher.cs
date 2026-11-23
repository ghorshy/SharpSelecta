using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;

namespace SharpSelecta.App.Services.Tray;

// Whether there's a tray to hide the window to. Windows always has one; on Linux it's whatever shows
// StatusNotifierItems (waybar's tray, a shell's panel), which can come and go while the app runs.
public sealed class TrayHostWatcher(ILogger<TrayHostWatcher> logger) : IDisposable
{
    private const string WatcherName = "org.kde.StatusNotifierWatcher";

    private DBusConnection? _connection;
    private NameOwnerWatcher? _watcher;

    public bool IsAvailable => !OperatingSystem.IsLinux() || _watcher?.GetCurrentOwner() is not null;

    public async Task StartAsync()
    {
        if (!OperatingSystem.IsLinux() || DBusAddress.Session is not { } address)
            return;

        try
        {
            _connection = new DBusConnection(address);
            await _connection.ConnectAsync();
            _watcher = await _connection.WatchNameOwnerAsync(WatcherName);
        }
        catch (Exception ex) when (ex is DBusMessageException or DBusConnectionException or IOException)
        {
            logger.LogWarning(ex, "Can't look for a system tray; the window won't hide to one");
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _connection?.Dispose();
    }
}
