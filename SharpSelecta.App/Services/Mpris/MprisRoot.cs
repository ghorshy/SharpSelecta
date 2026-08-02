using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using SharpSelecta.App.ViewModels;
using Tmds.DBus;

namespace SharpSelecta.App.Services.Mpris;

// A single object implementing BOTH org.mpris.MediaPlayer2 and org.mpris.MediaPlayer2.Player at
// /org/mpris/MediaPlayer2 - confirmed via a throwaway diagnostic against a real session bus that
// this has to be one object, not two registered separately at the same path (Tmds.DBus's
// RegisterObjectsAsync keys its internal handler table by ObjectPath and throws on a duplicate).
// Explicit interface implementation disambiguates the two interfaces' identically-named property
// boilerplate (GetAsync/GetAllAsync/SetAsync/WatchPropertiesAsync).
//
// Every PlaybackControlsViewModel read/write is marshaled onto the Avalonia UI thread - D-Bus
// method calls and property queries arrive on Tmds.DBus's own connection thread, not the UI
// thread, matching this codebase's existing convention for cross-thread ViewModel access (see
// LibraryViewModel.LoadAlbumArtworkAsync).
public sealed class MprisRoot : IMediaPlayer2, IMediaPlayer2Player, IDisposable
{
    private readonly PlaybackControlsViewModel _playbackControls;
    private Action<PropertyChanges>? _playerPropertiesChanged;
    private Action<long>? _seeked;

    public ObjectPath ObjectPath { get; } = new("/org/mpris/MediaPlayer2");

    public MprisRoot(PlaybackControlsViewModel playbackControls)
    {
        _playbackControls = playbackControls;
        _playbackControls.PropertyChanged += OnPlaybackControlsPropertyChanged;
    }

    public void Dispose() => _playbackControls.PropertyChanged -= OnPlaybackControlsPropertyChanged;

    private void OnPlaybackControlsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(PlaybackControlsViewModel.IsPlaying)
            or nameof(PlaybackControlsViewModel.TransportState)
            or nameof(PlaybackControlsViewModel.CurrentTrack)))
        {
            return;
        }

        _playerPropertiesChanged?.Invoke(new PropertyChanges(
            [
                new KeyValuePair<string, object>("PlaybackStatus", MprisMapping.PlaybackStatus(_playbackControls.TransportState, _playbackControls.IsPlaying)),
                new KeyValuePair<string, object>("Metadata", MprisMapping.BuildMetadata(_playbackControls.CurrentTrack)),
            ],
            []));
    }

    // --- org.mpris.MediaPlayer2 ---
    // Raise/Quit/DesktopEntry/UriSchemes/MimeTypes are all fixed "not supported" values - the base
    // interface still has to exist (playerctl's metadata/status commands silently fail to find the
    // player at all without it, confirmed via the same diagnostic), even though SharpSelecta has no
    // window-raise or quit-from-tray affordance to wire up yet.

    Task IMediaPlayer2.RaiseAsync() => Task.CompletedTask;

    Task IMediaPlayer2.QuitAsync() => Task.CompletedTask;

    Task<object> IMediaPlayer2.GetAsync(string prop) => Task.FromResult(GetMediaPlayer2Property(prop));

    Task<IDictionary<string, object>> IMediaPlayer2.GetAllAsync() => Task.FromResult(AllMediaPlayer2Properties);

    Task IMediaPlayer2.SetAsync(string prop, object val) => Task.CompletedTask;

    Task<IDisposable> IMediaPlayer2.WatchPropertiesAsync(Action<PropertyChanges> handler) =>
        Task.FromResult<IDisposable>(new NoopSubscription());

    private static object GetMediaPlayer2Property(string prop) => AllMediaPlayer2Properties.TryGetValue(prop, out var value)
        ? value
        : throw new DBusException("org.freedesktop.DBus.Error.UnknownProperty", $"Unknown property {prop}");

    private static IDictionary<string, object> AllMediaPlayer2Properties { get; } = new Dictionary<string, object>
    {
        ["CanQuit"] = false,
        ["CanRaise"] = false,
        ["HasTrackList"] = false,
        ["Identity"] = "SharpSelecta",
        ["DesktopEntry"] = "",
        ["SupportedUriSchemes"] = Array.Empty<string>(),
        ["SupportedMimeTypes"] = Array.Empty<string>(),
    };

    // --- org.mpris.MediaPlayer2.Player ---

    Task IMediaPlayer2Player.NextAsync() =>
        Dispatcher.UIThread.InvokeAsync(async () => await _playbackControls.NextTrackCommand.ExecuteAsync(null));

    Task IMediaPlayer2Player.PreviousAsync() =>
        Dispatcher.UIThread.InvokeAsync(async () => await _playbackControls.PreviousTrackCommand.ExecuteAsync(null));

    Task IMediaPlayer2Player.PauseAsync() => Dispatcher.UIThread.InvokeAsync(() =>
    {
        if (_playbackControls.IsPlaying)
        {
            _playbackControls.PlayPauseCommand.Execute(null);
        }
    }).GetTask();

    Task IMediaPlayer2Player.PlayAsync() => Dispatcher.UIThread.InvokeAsync(() =>
    {
        if (!_playbackControls.IsPlaying)
        {
            _playbackControls.PlayPauseCommand.Execute(null);
        }
    }).GetTask();

    Task IMediaPlayer2Player.PlayPauseAsync() => Dispatcher.UIThread.InvokeAsync(() => _playbackControls.PlayPauseCommand.Execute(null)).GetTask();

    // No distinct "stopped" transport beyond paused - the closest honest mapping of MPRIS Stop is
    // just ensuring playback is paused.
    Task IMediaPlayer2Player.StopAsync() => ((IMediaPlayer2Player)this).PauseAsync();

    Task IMediaPlayer2Player.SeekAsync(long offsetMicroseconds) => Dispatcher.UIThread.InvokeAsync(() =>
    {
        var newPosition = Math.Clamp(_playbackControls.PositionSeconds + offsetMicroseconds / 1_000_000.0, 0, _playbackControls.DurationSeconds);
        _playbackControls.PositionSeconds = newPosition;
        _seeked?.Invoke((long)(newPosition * 1_000_000));
    }).GetTask();

    Task IMediaPlayer2Player.SetPositionAsync(ObjectPath trackId, long positionMicroseconds) => Dispatcher.UIThread.InvokeAsync(() =>
    {
        // A stale TrackId means the client's view of "current track" is out of date - per spec,
        // just ignore the call rather than seeking the wrong (current) track to a position that
        // made sense for a track that's no longer loaded.
        if (_playbackControls.CurrentTrack is not { } track || MprisMapping.TrackId(track) != trackId)
        {
            return;
        }

        var newPosition = Math.Clamp(positionMicroseconds / 1_000_000.0, 0, _playbackControls.DurationSeconds);
        _playbackControls.PositionSeconds = newPosition;
        _seeked?.Invoke((long)(newPosition * 1_000_000));
    }).GetTask();

    Task IMediaPlayer2Player.OpenUriAsync(string uri) => Task.CompletedTask;

    Task<IDisposable> IMediaPlayer2Player.WatchSeekedAsync(Action<long> handler)
    {
        _seeked += handler;
        return Task.FromResult<IDisposable>(new Unsubscriber(() => _seeked -= handler));
    }

    Task<object> IMediaPlayer2Player.GetAsync(string prop) =>
        Dispatcher.UIThread.InvokeAsync(() => GetPlayerProperty(prop)).GetTask();

    Task<IDictionary<string, object>> IMediaPlayer2Player.GetAllAsync() =>
        Dispatcher.UIThread.InvokeAsync(GetAllPlayerProperties).GetTask();

    Task IMediaPlayer2Player.SetAsync(string prop, object val) => Task.CompletedTask;

    Task<IDisposable> IMediaPlayer2Player.WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        _playerPropertiesChanged += handler;
        return Task.FromResult<IDisposable>(new Unsubscriber(() => _playerPropertiesChanged -= handler));
    }

    // Must run on the UI thread - reads several PlaybackControlsViewModel properties together.
    private object GetPlayerProperty(string prop)
    {
        var properties = GetAllPlayerProperties();
        return properties.TryGetValue(prop, out var value)
            ? value
            : throw new DBusException("org.freedesktop.DBus.Error.UnknownProperty", $"Unknown property {prop}");
    }

    // Must run on the UI thread - reads several PlaybackControlsViewModel properties together.
    private IDictionary<string, object> GetAllPlayerProperties()
    {
        var hasTrack = _playbackControls.CurrentTrack is not null;
        return new Dictionary<string, object>
        {
            ["PlaybackStatus"] = MprisMapping.PlaybackStatus(_playbackControls.TransportState, _playbackControls.IsPlaying),
            ["Metadata"] = MprisMapping.BuildMetadata(_playbackControls.CurrentTrack),
            ["Position"] = (long)(_playbackControls.PositionSeconds * 1_000_000),
            ["Rate"] = 1.0,
            ["MinimumRate"] = 1.0,
            ["MaximumRate"] = 1.0,
            ["CanGoNext"] = _playbackControls.NextTrackCommand.CanExecute(null),
            ["CanGoPrevious"] = _playbackControls.PreviousTrackCommand.CanExecute(null),
            ["CanPlay"] = hasTrack,
            ["CanPause"] = hasTrack,
            ["CanSeek"] = hasTrack,
            ["CanControl"] = true,
        };
    }

    private sealed class Unsubscriber(Action action) : IDisposable
    {
        public void Dispose() => action();
    }

    // WatchPropertiesAsync on the base interface has nothing to ever notify (its properties are
    // all fixed), but still needs to return a disposable subscription per the interface contract.
    private sealed class NoopSubscription : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
