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
    // playerctld (and similar media-key routers) rank the "active" player by recency: whichever
    // MPRIS player most recently emitted a PropertiesChanged carrying a real value delta wins the
    // next media-key press, and that ranking can be stolen back by another player's own unrelated
    // D-Bus activity (a browser tab's metadata refresh, a new tab registering) at any point during
    // a track SharpSelecta is silently playing through - PropertiesChanged here is otherwise only
    // ever raised at Play/Pause/track-change transitions. This periodic nudge re-asserts priority
    // throughout playback instead of just at transitions. Position changes don't count towards
    // that recency check (deliberately excluded by playerctld, since every player's position ticks
    // constantly), hence the Metadata heartbeat key instead of relying on Position/Seeked.
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    private readonly PlaybackControlsViewModel _playbackControls;
    private readonly DispatcherTimer _heartbeatTimer;
    private long _heartbeatTick;
    private Action<PropertyChanges>? _playerPropertiesChanged;
    private Action<long>? _seeked;

    public ObjectPath ObjectPath { get; } = new("/org/mpris/MediaPlayer2");

    public MprisRoot(PlaybackControlsViewModel playbackControls)
    {
        _playbackControls = playbackControls;
        _playbackControls.PropertyChanged += OnPlaybackControlsPropertyChanged;

        _heartbeatTimer = new DispatcherTimer(HeartbeatInterval, DispatcherPriority.Background, OnHeartbeatTick);
    }

    public void Dispose()
    {
        _playbackControls.PropertyChanged -= OnPlaybackControlsPropertyChanged;
        _heartbeatTimer.Stop();
    }

    private void OnPlaybackControlsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(PlaybackControlsViewModel.IsPlaying)
            or nameof(PlaybackControlsViewModel.TransportState)
            or nameof(PlaybackControlsViewModel.CurrentTrack)))
        {
            return;
        }

        EmitPlayerPropertiesChanged();
    }

    // Only nudges while actually playing - a paused/stopped SharpSelecta has no stronger claim to
    // the media keys than anything else sitting idle on the bus.
    private void OnHeartbeatTick(object? sender, EventArgs e)
    {
        if (!_playbackControls.IsPlaying)
        {
            return;
        }

        NudgePriority();
    }

    // Unconditional (unlike the heartbeat): focusing the window is itself the user's claim on the
    // media keys, playing or not. Must be called on the UI thread (reads _playbackControls), which
    // Window.Activated - the one caller - already guarantees.
    public void NudgePriority()
    {
        _heartbeatTick++;
        EmitPlayerPropertiesChanged(_heartbeatTick);
    }

    private void EmitPlayerPropertiesChanged(long? heartbeatTick = null) =>
        _playerPropertiesChanged?.Invoke(new PropertyChanges(
            [
                new KeyValuePair<string, object>("PlaybackStatus", MprisMapping.PlaybackStatus(_playbackControls.TransportState, _playbackControls.IsPlaying)),
                new KeyValuePair<string, object>("Metadata", MprisMapping.BuildMetadata(_playbackControls.CurrentTrack, heartbeatTick)),
            ],
            []));

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

    private static object GetMediaPlayer2Property(string prop) => GetPropertyOrThrow(AllMediaPlayer2Properties, prop);

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
        Dispatcher.UIThread.InvokeAsync(() => _playbackControls.NextTrackCommand.ExecuteAsync(null));

    Task IMediaPlayer2Player.PreviousAsync() =>
        Dispatcher.UIThread.InvokeAsync(() => _playbackControls.PreviousTrackCommand.ExecuteAsync(null));

    Task IMediaPlayer2Player.PauseAsync() => Dispatcher.UIThread.InvokeAsync(() =>
    {
        if (_playbackControls.IsPlaying)
        {
            _playbackControls.PlayPauseCommand.Execute(null);
        }
    }).GetTask();

    Task IMediaPlayer2Player.PlayAsync() => Dispatcher.UIThread.InvokeAsync(() =>
        _playbackControls.IsPlaying ? Task.CompletedTask : ToggleOrStartPlaybackAsync());

    Task IMediaPlayer2Player.PlayPauseAsync() => Dispatcher.UIThread.InvokeAsync(ToggleOrStartPlaybackAsync);

    // PlayPauseCommand only resumes an already-loaded track (gated on TransportState == Ready) -
    // it silently no-ops with nothing loaded yet, e.g. tracks added via "Add to Queue" but never
    // actually played this session. Falling through to Next mirrors what pressing Next already does
    // from that same cold-start state (advances from CurrentIndex -1 to the first queued track and
    // plays it) - without this, CanPlay had to report false whenever CurrentTrack was null even
    // though the queue had something to play, which made playerctl skip SharpSelecta for Play/
    // PlayPause specifically (it checks CanPlay/CanPause before sending them) while Next/Previous -
    // gated only on the queue, not on CurrentTrack - still worked. See CanPlay below.
    private Task ToggleOrStartPlaybackAsync()
    {
        if (_playbackControls.CurrentTrack is null)
        {
            return _playbackControls.NextTrackCommand.ExecuteAsync(null);
        }

        _playbackControls.PlayPauseCommand.Execute(null);
        return Task.CompletedTask;
    }

    // No distinct "stopped" transport beyond paused - the closest honest mapping of MPRIS Stop is
    // just ensuring playback is paused.
    Task IMediaPlayer2Player.StopAsync() => ((IMediaPlayer2Player)this).PauseAsync();

    Task IMediaPlayer2Player.SeekAsync(long offsetMicroseconds) => Dispatcher.UIThread.InvokeAsync(() =>
        ApplySeek(_playbackControls.PositionSeconds + offsetMicroseconds / 1_000_000.0)).GetTask();

    Task IMediaPlayer2Player.SetPositionAsync(ObjectPath trackId, long positionMicroseconds) => Dispatcher.UIThread.InvokeAsync(() =>
    {
        // A stale TrackId means the client's view of "current track" is out of date - per spec,
        // just ignore the call rather than seeking the wrong (current) track to a position that
        // made sense for a track that's no longer loaded.
        if (_playbackControls.CurrentTrack is not { } track || MprisMapping.TrackId(track) != trackId)
        {
            return;
        }

        ApplySeek(positionMicroseconds / 1_000_000.0);
    }).GetTask();

    private void ApplySeek(double targetSeconds)
    {
        var newPosition = Math.Clamp(targetSeconds, 0, _playbackControls.DurationSeconds);
        _playbackControls.PositionSeconds = newPosition;
        _seeked?.Invoke((long)(newPosition * 1_000_000));
    }

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
    private object GetPlayerProperty(string prop) => GetPropertyOrThrow(GetAllPlayerProperties(), prop);

    private static object GetPropertyOrThrow(IDictionary<string, object> properties, string prop) =>
        properties.TryGetValue(prop, out var value)
            ? value
            : throw new DBusException("org.freedesktop.DBus.Error.UnknownProperty", $"Unknown property {prop}");

    // Must run on the UI thread - reads several PlaybackControlsViewModel properties together.
    private IDictionary<string, object> GetAllPlayerProperties()
    {
        var canGoNext = _playbackControls.NextTrackCommand.CanExecute(null);
        var canResume = _playbackControls.PlayPauseCommand.CanExecute(null);
        var hasTrack = _playbackControls.CurrentTrack is not null;

        return new Dictionary<string, object>
        {
            ["PlaybackStatus"] = MprisMapping.PlaybackStatus(_playbackControls.TransportState, _playbackControls.IsPlaying),
            ["Metadata"] = MprisMapping.BuildMetadata(_playbackControls.CurrentTrack),
            ["Position"] = (long)(_playbackControls.PositionSeconds * 1_000_000),
            ["Rate"] = 1.0,
            ["MinimumRate"] = 1.0,
            ["MaximumRate"] = 1.0,
            ["CanGoNext"] = canGoNext,
            ["CanGoPrevious"] = _playbackControls.PreviousTrackCommand.CanExecute(null),
            ["CanPlay"] = MprisMapping.CanPlay(canResume, hasTrack, canGoNext),
            ["CanPause"] = canResume,
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
