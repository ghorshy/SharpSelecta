using Microsoft.Extensions.Logging;
using SharpSelecta.App.ViewModels;
using Tmds.DBus.Protocol;

namespace SharpSelecta.App.Services.Mpris;

public sealed class MprisService : IAsyncDisposable
{
    private const string BusName = "org.mpris.MediaPlayer2.sharpselecta";

    private readonly DBusConnection _connection;
    private readonly MprisRoot _root;

    private MprisService(DBusConnection connection, MprisRoot root)
    {
        _connection = connection;
        _root = root;
    }

    public static async Task<MprisService?> TryStartAsync(PlaybackControlsViewModel playbackControls, ILogger logger)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        DBusConnection? connection = null;
        MprisRoot? root = null;
        try
        {
            if (DBusAddress.Session is not { } sessionAddress)
            {
                logger.LogWarning("No D-Bus session bus address available - MPRIS/playerctl integration will be unavailable");
                return null;
            }

            connection = new DBusConnection(sessionAddress);
            await connection.ConnectAsync();

            root = new MprisRoot(connection, playbackControls);
            connection.AddMethodHandler(root);
            await connection.RequestNameAsync(BusName, RequestNameOptions.None);

            logger.LogInformation("MPRIS service registered as {BusName}", BusName);
            return new MprisService(connection, root);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start the MPRIS D-Bus service - media-key/playerctl integration will be unavailable");
            root?.Dispose();
            connection?.Dispose();
            return null;
        }
    }

    public void NudgePriority() => _root.NudgePriority();

    public async ValueTask DisposeAsync()
    {
        _root.Dispose();
        try
        {
            await _connection.ReleaseNameAsync(BusName);
        }
        catch (Exception)
        {
        }

        _connection.Dispose();
    }
}
