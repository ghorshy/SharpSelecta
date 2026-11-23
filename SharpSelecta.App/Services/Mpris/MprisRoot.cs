using System.ComponentModel;
using Avalonia.Threading;
using SharpSelecta.App.DBus;
using SharpSelecta.App.ViewModels;
using Tmds.DBus.Protocol;

namespace SharpSelecta.App.Services.Mpris;

internal sealed class MprisRoot : DBusHandler, IMediaPlayer2Handler, IPlayerHandler, IMediaPlayer2Properties, IDisposable
{
    public const string ObjectPath = "/org/mpris/MediaPlayer2";

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    private readonly PlaybackControlsViewModel _playbackControls;
    private readonly DispatcherTimer _heartbeatTimer;
    private long _heartbeatTick;

    public MprisRoot(DBusConnection connection, PlaybackControlsViewModel playbackControls)
        : base(connection, ObjectPath, handlesChildPaths: false, handleOnCapturedContext: false)
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
        Connection.EmitPlayerPropertiesChanged(
            Path,
            playbackStatus: MprisMapping.PlaybackStatus(_playbackControls.TransportState, _playbackControls.IsPlaying),
            metadata: MprisMapping.BuildMetadata(_playbackControls.CurrentTrack, heartbeatTick));

    ValueTask IMediaPlayer2Handler.RaiseAsync() => default;

    ValueTask IMediaPlayer2Handler.QuitAsync() => default;

    ValueTask IMediaPlayer2Handler.HandleGetPropertyAsync(IMediaPlayer2Handler.GetPropertyContext context) =>
        context.Handle(this);

    ValueTask IMediaPlayer2Handler.HandleGetAllPropertiesAsync(IMediaPlayer2Handler.GetAllPropertiesContext context) =>
        context.Handle(this);

    bool IMediaPlayer2Properties.CanQuit => false;

    bool IMediaPlayer2Properties.CanRaise => false;

    bool IMediaPlayer2Properties.HasTrackList => false;

    string IMediaPlayer2Properties.Identity => "SharpSelecta";

    string IMediaPlayer2Properties.DesktopEntry => AppIdentity.Id;

    string[] IMediaPlayer2Properties.SupportedUriSchemes => [];

    string[] IMediaPlayer2Properties.SupportedMimeTypes => [];

    ValueTask IPlayerHandler.NextAsync() =>
        new(Dispatcher.UIThread.InvokeAsync(() => _playbackControls.NextTrackCommand.ExecuteAsync(null)));

    ValueTask IPlayerHandler.PreviousAsync() =>
        new(Dispatcher.UIThread.InvokeAsync(() => _playbackControls.PreviousTrackCommand.ExecuteAsync(null)));

    ValueTask IPlayerHandler.PauseAsync() => new(Dispatcher.UIThread.InvokeAsync(() =>
    {
        if (_playbackControls.IsPlaying)
        {
            _playbackControls.PlayPauseCommand.Execute(null);
        }
    }).GetTask());

    ValueTask IPlayerHandler.PlayAsync() => new(Dispatcher.UIThread.InvokeAsync(() =>
        _playbackControls.IsPlaying ? Task.CompletedTask : _playbackControls.PlayPauseCommand.ExecuteAsync(null)));

    ValueTask IPlayerHandler.PlayPauseAsync() =>
        new(Dispatcher.UIThread.InvokeAsync(() => _playbackControls.PlayPauseCommand.ExecuteAsync(null)));

    ValueTask IPlayerHandler.StopAsync() => ((IPlayerHandler)this).PauseAsync();

    ValueTask IPlayerHandler.SeekAsync(long offset) => new(Dispatcher.UIThread.InvokeAsync(() =>
        ApplySeek(_playbackControls.PositionSeconds + offset / 1_000_000.0)).GetTask());

    ValueTask IPlayerHandler.SetPositionAsync(Tmds.DBus.Protocol.ObjectPath trackId, long position) =>
        new(Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_playbackControls.CurrentTrack is not { } track || !MprisMapping.TrackId(track).Equals(trackId))
            {
                return;
            }

            ApplySeek(position / 1_000_000.0);
        }).GetTask());

    private void ApplySeek(double targetSeconds)
    {
        var newPosition = Math.Clamp(targetSeconds, 0, _playbackControls.DurationSeconds);
        _playbackControls.PositionSeconds = newPosition;
        Connection.EmitSeeked(Path, (long)(newPosition * 1_000_000));
    }

    ValueTask IPlayerHandler.OpenUriAsync(string uri) => default;

    async ValueTask IPlayerHandler.HandleGetPropertyAsync(IPlayerHandler.GetPropertyContext context) =>
        await context.Handle(await Dispatcher.UIThread.InvokeAsync(SnapshotPlayer));

    async ValueTask IPlayerHandler.HandleGetAllPropertiesAsync(IPlayerHandler.GetAllPropertiesContext context) =>
        await context.Handle(await Dispatcher.UIThread.InvokeAsync(SnapshotPlayer));

    // Rate is read-write in the spec, but only 1.0 is supported, so a write is accepted and ignored.
    ValueTask IPlayerHandler.HandleSetPropertyAsync(IPlayerHandler.SetPropertyContext context) =>
        context.Handle(new PlayerProperties());

    private PlayerProperties SnapshotPlayer() => new()
    {
        PlaybackStatus = MprisMapping.PlaybackStatus(_playbackControls.TransportState, _playbackControls.IsPlaying),
        Metadata = MprisMapping.BuildMetadata(_playbackControls.CurrentTrack),
        Position = (long)(_playbackControls.PositionSeconds * 1_000_000),
        CanGoNext = _playbackControls.NextTrackCommand.CanExecute(null),
        CanGoPrevious = _playbackControls.PreviousTrackCommand.CanExecute(null),
        CanPlay = _playbackControls.PlayPauseCommand.CanExecute(null),
        CanPause = _playbackControls.TransportState == TransportState.Ready,
        CanSeek = _playbackControls.CurrentTrack is not null,
    };

    private sealed class PlayerProperties : IPlayerProperties
    {
        public string PlaybackStatus { get; init; } = "Stopped";

        public double Rate
        {
            get => 1.0;
            set { }
        }

        public Dictionary<string, VariantValue> Metadata { get; init; } = [];

        public long Position { get; init; }

        public double MinimumRate => 1.0;

        public double MaximumRate => 1.0;

        public bool CanGoNext { get; init; }

        public bool CanGoPrevious { get; init; }

        public bool CanPlay { get; init; }

        public bool CanPause { get; init; }

        public bool CanSeek { get; init; }

        public bool CanControl => true;
    }
}
