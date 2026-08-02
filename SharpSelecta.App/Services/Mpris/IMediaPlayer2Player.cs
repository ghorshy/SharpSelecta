using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace SharpSelecta.App.Services.Mpris;

[DBusInterface("org.mpris.MediaPlayer2.Player")]
public interface IMediaPlayer2Player : IDBusObject
{
    Task NextAsync();

    Task PreviousAsync();

    Task PauseAsync();

    Task PlayPauseAsync();

    Task StopAsync();

    Task PlayAsync();

    Task SeekAsync(long offsetMicroseconds);

    Task SetPositionAsync(ObjectPath trackId, long positionMicroseconds);

    Task OpenUriAsync(string uri);

    Task<IDisposable> WatchSeekedAsync(Action<long> handler);

    Task<object> GetAsync(string prop);

    Task<IDictionary<string, object>> GetAllAsync();

    Task SetAsync(string prop, object val);

    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}
