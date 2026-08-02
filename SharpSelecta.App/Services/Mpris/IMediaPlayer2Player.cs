using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace SharpSelecta.App.Services.Mpris;

// org.mpris.MediaPlayer2.Player - the interface playerctl and Hyprland's media-key bindings
// actually drive. See https://specifications.freedesktop.org/mpris-spec/latest/Player_Interface.html.
[DBusInterface("org.mpris.MediaPlayer2.Player")]
public interface IMediaPlayer2Player : IDBusObject
{
    Task NextAsync();

    Task PreviousAsync();

    Task PauseAsync();

    Task PlayPauseAsync();

    Task StopAsync();

    Task PlayAsync();

    // Relative seek, offset in microseconds (matches the spec's Seek(x: Offset)).
    Task SeekAsync(long offsetMicroseconds);

    // Absolute seek, only applied if trackId still matches the currently loaded track (per spec -
    // a stale TrackId means the client's view of "current track" is out of date).
    Task SetPositionAsync(ObjectPath trackId, long positionMicroseconds);

    Task OpenUriAsync(string uri);

    Task<IDisposable> WatchSeekedAsync(Action<long> handler);

    Task<object> GetAsync(string prop);

    Task<IDictionary<string, object>> GetAllAsync();

    Task SetAsync(string prop, object val);

    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}
