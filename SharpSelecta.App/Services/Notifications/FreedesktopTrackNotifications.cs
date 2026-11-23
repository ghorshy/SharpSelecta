using System.Net;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Resources;
using NotificationsProxy = SharpSelecta.App.DBus.Notifications;
using Tmds.DBus.Protocol;

namespace SharpSelecta.App.Services.Notifications;

// Linux notifications over the session bus. Without a bus or a notification daemon it logs once and
// keeps trying on later tracks (a daemon may start after the app).
public sealed class FreedesktopTrackNotifications(ILogger<FreedesktopTrackNotifications> logger) : ITrackNotifications, IDisposable
{
    private const string SkipAction = "next";

    private static readonly ObjectPath NotificationsPath = new("/org/freedesktop/Notifications");

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _coverDirectory = Path.Combine(Path.GetTempPath(), $"sharpselecta-{Environment.UserName}-notifications");
    private DBusConnection? _connection;
    private NotificationsProxy? _notifications;
    private IDisposable? _actionWatch;
    private bool _bodyMarkup;
    private string[] _actions = [];
    private volatile uint _lastId;
    private string? _lastCoverPath;
    private bool _loggedFailure;

    public event EventHandler? SkipRequested;

    public static string[] ActionsFor(IReadOnlyCollection<string> capabilities) =>
        capabilities.Contains("actions") ? [SkipAction, Strings.NotificationSkip] : [];

    public static bool IsSkip(uint notificationId, string actionKey, uint latestId) =>
        notificationId == latestId && actionKey == SkipAction;

    public async Task ShowAsync(TrackNotification notification)
    {
        await _gate.WaitAsync();
        try
        {
            if (await ConnectAsync() is not { } notifications)
                return;

            var hints = new Dictionary<string, VariantValue> { ["desktop-entry"] = AppIdentity.Id, ["category"] = "x-sharpselecta.track" };
            if (WriteCover(notification.Artwork) is { } coverPath)
                hints["image-path"] = new Uri(coverPath).AbsoluteUri;

            var body = _bodyMarkup ? WebUtility.HtmlEncode(notification.Body) : notification.Body;
            _lastId = await notifications.NotifyAsync("SharpSelecta", _lastId, string.Empty, notification.Title, body, _actions, hints, -1);
        }
        catch (Exception ex) when (ex is DBusMessageException or DBusConnectionException or IOException or UnauthorizedAccessException)
        {
            LogFailureOnce(ex);
            Disconnect();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<NotificationsProxy?> ConnectAsync()
    {
        if (_notifications is not null)
            return _notifications;

        if (DBusAddress.Session is not { } address)
        {
            LogFailureOnce(null);
            return null;
        }

        _connection = new DBusConnection(address);
        await _connection.ConnectAsync();
        var notifications = new NotificationsProxy(_connection, NotificationsProxy.DBusInterfaceName, NotificationsPath);
        var capabilities = await notifications.GetCapabilitiesAsync();
        _bodyMarkup = capabilities.Contains("body-markup");
        _actions = ActionsFor(capabilities);
        if (_actions.Length > 0)
            _actionWatch = await notifications.WatchActionInvokedAsync(OnActionInvoked, emitOnCapturedContext: false);
        _notifications = notifications;
        return notifications;
    }

    // A file per notification: some daemons read the image only when they draw it, so the previous
    // one is deleted only once the next has been written.
    private string? WriteCover(byte[]? artwork)
    {
        if (artwork is not { Length: > 0 })
            return null;

        Directory.CreateDirectory(_coverDirectory);
        var isPng = artwork is [0x89, (byte)'P', (byte)'N', (byte)'G', ..];
        var path = Path.Combine(_coverDirectory, $"{Guid.NewGuid():N}{(isPng ? ".png" : ".jpg")}");
        File.WriteAllBytes(path, artwork);

        DeleteCover(_lastCoverPath);
        _lastCoverPath = path;
        return path;
    }

    private static void DeleteCover(string? path)
    {
        if (path is null)
            return;

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private void LogFailureOnce(Exception? ex)
    {
        if (_loggedFailure)
            return;

        _loggedFailure = true;
        logger.LogWarning(ex, "Desktop notifications are unavailable (no session bus or notification daemon)");
    }

    private void OnActionInvoked((uint Id, string ActionKey) action)
    {
        if (IsSkip(action.Id, action.ActionKey, _lastId))
            SkipRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Disconnect()
    {
        _actionWatch?.Dispose();
        _actionWatch = null;
        _connection?.Dispose();
        _connection = null;
        _notifications = null;
    }

    public void Dispose()
    {
        Disconnect();
        DeleteCover(_lastCoverPath);
        _gate.Dispose();
    }
}
