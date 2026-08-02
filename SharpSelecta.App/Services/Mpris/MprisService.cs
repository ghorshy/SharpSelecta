using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.ViewModels;
using Tmds.DBus;

namespace SharpSelecta.App.Services.Mpris;

// D-Bus/MPRIS only exists on Linux (and BSD, but SharpSelecta doesn't target that) - this is what
// lets playerctl and Hyprland's/GNOME's/KDE's global media-key bindings control playback. Never
// lets a missing/unavailable session bus (or anything else D-Bus related) crash startup: every
// failure is caught and logged, and the app just runs without remote-control integration.
public sealed class MprisService : IAsyncDisposable
{
    private const string BusName = "org.mpris.MediaPlayer2.sharpselecta";

    private readonly Connection _connection;
    private readonly MprisRoot _root;

    private MprisService(Connection connection, MprisRoot root)
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

        Connection? connection = null;
        try
        {
            if (Address.Session is not { } sessionAddress)
            {
                logger.LogWarning("No D-Bus session bus address available - MPRIS/playerctl integration will be unavailable");
                return null;
            }

            connection = new Connection(sessionAddress);
            await connection.ConnectAsync();

            var root = new MprisRoot(playbackControls);
            await connection.RegisterObjectAsync(root);
            await connection.RegisterServiceAsync(BusName, new ServiceRegistrationOptions());

            logger.LogInformation("MPRIS service registered as {BusName}", BusName);
            return new MprisService(connection, root);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start the MPRIS D-Bus service - media-key/playerctl integration will be unavailable");
            connection?.Dispose();
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _root.Dispose();
        try
        {
            await _connection.UnregisterServiceAsync(BusName);
        }
        catch (Exception)
        {
            // Best-effort - the connection is being torn down either way right after.
        }

        _connection.Dispose();
    }
}
