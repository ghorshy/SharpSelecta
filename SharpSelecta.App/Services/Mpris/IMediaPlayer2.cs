using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace SharpSelecta.App.Services.Mpris;

// The base MPRIS interface every media player exposes at /org/mpris/MediaPlayer2, independent of
// the actual Player interface below. See https://specifications.freedesktop.org/mpris-spec/latest/.
[DBusInterface("org.mpris.MediaPlayer2")]
public interface IMediaPlayer2 : IDBusObject
{
    Task RaiseAsync();

    Task QuitAsync();

    Task<object> GetAsync(string prop);

    Task<IDictionary<string, object>> GetAllAsync();

    Task SetAsync(string prop, object val);

    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}
