using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using SharpSelecta.App.ViewModels;
using Tmds.DBus;

namespace SharpSelecta.App.Services.Mpris;

public sealed class MprisRoot : IMediaPlayer2, IMediaPlayer2Player, IDisposable
{
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

    private void OnHeartbeatTick(object? sender, EventArgs e)
    {
        if (!_playbackControls.IsPlaying)
        {
            return;
        }

        NudgePriority();
    }

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

    private Task ToggleOrStartPlaybackAsync()
    {
        if (_playbackControls.CurrentTrack is null)
        {
            return _playbackControls.NextTrackCommand.ExecuteAsync(null);
        }

        _playbackControls.PlayPauseCommand.Execute(null);
        return Task.CompletedTask;
    }

    Task IMediaPlayer2Player.StopAsync() => ((IMediaPlayer2Player)this).PauseAsync();

    Task IMediaPlayer2Player.SeekAsync(long offsetMicroseconds) => Dispatcher.UIThread.InvokeAsync(() =>
        ApplySeek(_playbackControls.PositionSeconds + offsetMicroseconds / 1_000_000.0)).GetTask();

    Task IMediaPlayer2Player.SetPositionAsync(ObjectPath trackId, long positionMicroseconds) => Dispatcher.UIThread.InvokeAsync(() =>
    {
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

    private object GetPlayerProperty(string prop) => GetPropertyOrThrow(GetAllPlayerProperties(), prop);

    private static object GetPropertyOrThrow(IDictionary<string, object> properties, string prop) =>
        properties.TryGetValue(prop, out var value)
            ? value
            : throw new DBusException("org.freedesktop.DBus.Error.UnknownProperty", $"Unknown property {prop}");

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

    private sealed class NoopSubscription : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
