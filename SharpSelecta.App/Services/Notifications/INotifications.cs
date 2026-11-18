using Tmds.DBus;

namespace SharpSelecta.App.Services.Notifications;

// https://specifications.freedesktop.org/notification-spec/latest/
[DBusInterface("org.freedesktop.Notifications")]
public interface INotifications : IDBusObject
{
    Task<string[]> GetCapabilitiesAsync();

    Task<uint> NotifyAsync(
        string appName, uint replacesId, string appIcon, string summary, string body,
        string[] actions, IDictionary<string, object> hints, int expireTimeout);
}
