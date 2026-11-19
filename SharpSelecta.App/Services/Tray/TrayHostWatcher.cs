using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace SharpSelecta.App.Services.Tray;

// Whether there's a tray to hide the window to. Windows always has one; on Linux it's whatever shows
// StatusNotifierItems (waybar's tray, a shell's panel), which can come and go while the app runs.
public sealed class TrayHostWatcher(ILogger<TrayHostWatcher> logger) : IDisposable
{
    private const string WatcherName = "org.kde.StatusNotifierWatcher";

    private Connection? _connection;
    private IDisposable? _subscription;
    private volatile bool _isAvailable = !OperatingSystem.IsLinux();

    public bool IsAvailable => _isAvailable;

    public async Task StartAsync()
    {
        if (!OperatingSystem.IsLinux() || Address.Session is not { } address)
            return;

        try
        {
            _connection = new Connection(address);
            await _connection.ConnectAsync();
            _subscription = await _connection.ResolveServiceOwnerAsync(WatcherName, e => _isAvailable = e.NewOwner is not null,
                ex => logger.LogWarning(ex, "Lost track of the system tray"));
            _isAvailable = await _connection.IsServiceActiveAsync(WatcherName);
        }
        catch (Exception ex) when (ex is DBusException or ConnectException or DisconnectedException or IOException)
        {
            logger.LogWarning(ex, "Can't look for a system tray; the window won't hide to one");
        }
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _connection?.Dispose();
    }
}
