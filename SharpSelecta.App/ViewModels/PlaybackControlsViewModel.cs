using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.App.ViewModels;

public partial class PlaybackControlsViewModel : ViewModelBase
{
    // Matches the common "restart vs. go back" convention (Spotify, Winamp, etc.): a deliberate
    // press of Previous soon after starting a track means "go back," but once you're a few
    // seconds in, it more likely means "restart this one" than "skip it entirely."
    private const double RestartThresholdSeconds = 3.0;

    private readonly IAudioEngine _audioEngine;
    private readonly PlaybackQueue _queue;
    private readonly ILogger<PlaybackControlsViewModel> _logger;
    private bool _isSyncingFromEngine;
    private bool _hasHandledEndOfStream;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    private TransportState transportState = TransportState.NoTrack;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseLabel))]
    private bool isPlaying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionDisplay))]
    [NotifyPropertyChangedFor(nameof(DurationDisplay))]
    private double positionSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationDisplay))]
    private double durationSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationDisplay))]
    private bool showRemainingTime;

    [ObservableProperty]
    private double volume = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayFileName))]
    private string? loadedFileName;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private Track? currentTrack;

    // Raw bytes rather than an Avalonia Bitmap — constructing a Bitmap requires the platform's
    // rendering backend to be initialized, which a plain unit test process doesn't have. The View
    // converts these bytes to a Bitmap at render time instead (see ArtworkConverter).
    [ObservableProperty]
    private byte[]? currentTrackArtworkBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RepeatModeLabel))]
    private RepeatMode repeatMode = RepeatMode.Off;

    public PlaybackControlsViewModel(IAudioEngine audioEngine, PlaybackQueue queue, ILogger<PlaybackControlsViewModel> logger)
    {
        _audioEngine = audioEngine;
        _queue = queue;
        _logger = logger;

        // Keeps Previous/Next enabled state in sync as the queue is edited or navigated,
        // whether that happens here or from the Library's context menu. ReadOnlyObservableCollection
        // implements CollectionChanged explicitly, hence the interface cast.
        ((INotifyCollectionChanged)_queue.Entries).CollectionChanged += (_, _) => RefreshNavigationCommands();
        _queue.CurrentIndexChanged += (_, _) =>
        {
            RefreshNavigationCommands();
            OnPropertyChanged(nameof(QueueCurrentIndex));
        };
    }

    // This class is the single owner of the shared PlaybackQueue — the Library and Queue
    // components only ever reach it through these members, never touching PlaybackQueue directly.
    public ReadOnlyObservableCollection<QueueEntry> QueueEntries => _queue.Entries;

    public int QueueCurrentIndex => _queue.CurrentIndex;

    public void PlayNext(Track track) => _queue.PlayNext(track);

    public void AddToQueue(Track track) => _queue.AddToQueue(track);

    // Dropping onto another entry lands the dragged entry immediately above it; dropping with no
    // target (targetEntry is null, e.g. past the last row) moves it to the end of the queue instead.
    public void MoveQueueEntry(QueueEntry entry, QueueEntry? targetEntry)
    {
        var oldIndex = _queue.IndexOf(entry);
        if (oldIndex < 0)
            return;

        int newIndex;
        if (targetEntry is null)
        {
            newIndex = _queue.Entries.Count - 1;
        }
        else
        {
            var targetIndex = _queue.IndexOf(targetEntry);
            if (targetIndex < 0)
                return;

            // PlaybackQueue.Move's newIndex is interpreted against the list AFTER the source entry
            // is removed. When dragging downward (oldIndex < targetIndex), the target has already
            // shifted left by one by the time the insert happens, so landing "immediately above the
            // target" — matching the Queue view's drop indicator — means using targetIndex - 1, not
            // targetIndex. Dragging upward needs no adjustment: removing from below the target never
            // moves it.
            newIndex = oldIndex < targetIndex ? targetIndex - 1 : targetIndex;
        }

        _queue.Move(oldIndex, newIndex);
    }

    public void RemoveFromQueue(QueueEntry entry)
    {
        var index = _queue.IndexOf(entry);
        if (index >= 0)
        {
            _queue.RemoveAt(index);
        }
    }

    public async Task PlayQueueEntryAsync(QueueEntry entry)
    {
        var index = _queue.IndexOf(entry);
        if (index < 0)
            return;

        var track = _queue.JumpTo(index);
        if (track is not null)
        {
            await LoadTrackAsync(track);
        }
    }

    public string PlayPauseLabel => IsPlaying ? Strings.Pause : Strings.Play;

    public string DisplayFileName => LoadedFileName ?? Strings.NoFileLoaded;

    public string PositionDisplay => FormatTime(PositionSeconds);

    // Toggled by clicking it: total duration, or time remaining (prefixed with "-"), like
    // Spotify/Apple Music's duration-label toggle.
    public string DurationDisplay => ShowRemainingTime
        ? $"-{FormatTime(Math.Max(0, DurationSeconds - PositionSeconds))}"
        : FormatTime(DurationSeconds);

    [RelayCommand]
    private void ToggleDurationDisplay() => ShowRemainingTime = !ShowRemainingTime;

    private static string FormatTime(double totalSeconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return span.Hours > 0 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
    }

    public string RepeatModeLabel => RepeatMode switch
    {
        RepeatMode.RepeatAll => Strings.RepeatAll,
        RepeatMode.RepeatOne => Strings.RepeatOne,
        _ => Strings.RepeatOff,
    };

    // Cycles Off -> Repeat All -> Repeat One -> Off, matching SoundCloud's repeat toggle.
    [RelayCommand]
    private void ToggleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.RepeatAll,
            RepeatMode.RepeatAll => RepeatMode.RepeatOne,
            _ => RepeatMode.Off,
        };
    }

    [RelayCommand(CanExecute = nameof(CanPlayPause))]
    private void PlayPause()
    {
        if (IsPlaying)
        {
            _audioEngine.Pause();
            IsPlaying = false;
        }
        else
        {
            _audioEngine.Play();
            IsPlaying = true;
        }
    }

    // Nothing to play/pause, go back to, or restart until something has actually been loaded.
    private bool HasCurrentTrack() => _queue.CurrentIndex >= 0;

    private bool CanPlayPause() => TransportState == TransportState.Ready;

    [RelayCommand(CanExecute = nameof(HasCurrentTrack))]
    private async Task PreviousTrackAsync()
    {
        if (PositionSeconds > RestartThresholdSeconds)
        {
            RestartCurrentTrack();
            return;
        }

        var track = _queue.MovePrevious();
        if (track is not null)
        {
            await LoadTrackAsync(track);
        }
        else
        {
            // Already at the start of history — nowhere to go back to, so restart instead.
            RestartCurrentTrack();
        }
    }

    private void RestartCurrentTrack()
    {
        PositionSeconds = 0;
        TransportState = TransportState.Ready;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextTrackAsync()
    {
        var track = _queue.MoveNext();
        if (track is not null)
        {
            await LoadTrackAsync(track);
        }
    }

    private bool CanGoNext() => _queue.CanGoNext;

    private void RefreshNavigationCommands()
    {
        PlayPauseCommand.NotifyCanExecuteChanged();
        PreviousTrackCommand.NotifyCanExecuteChanged();
        NextTrackCommand.NotifyCanExecuteChanged();
    }

    // Entry point for playing an arbitrary track "right now" (Library's Play Now/double-click) —
    // it joins the queue right after the current position so it shows up (and is browsable via
    // Previous/Next) alongside everything else, instead of bypassing the queue entirely.
    public async Task PlayNowAsync(Track track)
    {
        _queue.PlayNow(track);
        await LoadTrackAsync(track);
    }

    // Public so anything that has already positioned the queue (Next/Previous, PlayNowAsync,
    // or the Queue view's own jump-to-entry) can trigger the actual load+play mechanics without
    // also mutating the queue itself.
    public async Task LoadTrackAsync(Track track)
    {
        try
        {
            await Task.Run(() => _audioEngine.Load(track.FilePath));
            LoadedFileName = track.DisplayName;
            StatusMessage = null;
            IsPlaying = false;
            _hasHandledEndOfStream = false;
            TransportState = TransportState.Ready;
            CurrentTrack = track;
            CurrentTrackArtworkBytes = await Task.Run(() => MusicLibraryScanner.LoadArtwork(track.FilePath));
            RefreshPosition();
            PlayPauseCommand.Execute(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load {FilePath}", track.FilePath);
            StatusMessage = Strings.FailedToLoadFile(ex.Message);
        }
    }

    partial void OnPositionSecondsChanged(double value)
    {
        if (_isSyncingFromEngine)
            return;

        _audioEngine.Seek(value);
    }

    partial void OnVolumeChanged(double value) => _audioEngine.Volume = (float)value;

    public void RefreshPosition() => _ = RefreshPositionAsync();

    // Awaitable form of RefreshPosition() so callers (tests, in particular) can deterministically
    // wait for any end-of-stream reaction it triggers instead of racing a fire-and-forget Task.
    public async Task RefreshPositionAsync()
    {
        _isSyncingFromEngine = true;
        PositionSeconds = _audioEngine.Position;
        DurationSeconds = _audioEngine.Duration;
        _isSyncingFromEngine = false;

        // OwnAudioSharp's IsEndOfStream/StateChanged(EndOfStream) never fire on this preview
        // build — Position just keeps climbing past Duration indefinitely instead (confirmed via
        // a throwaway diagnostic). Position >= Duration is the only reliable "track finished"
        // signal available, so we detect it that way instead.
        if (DurationSeconds > 0 && PositionSeconds >= DurationSeconds && !_hasHandledEndOfStream)
        {
            _hasHandledEndOfStream = true;
            await HandleTrackEndedAsync();
        }
    }

    private async Task HandleTrackEndedAsync()
    {
        if (RepeatMode == RepeatMode.RepeatOne)
        {
            RestartCurrentTrack();
            _audioEngine.Play();
            IsPlaying = true;
            // Unlike the other branches, this doesn't go through LoadTrackAsync (which is what
            // normally resets this flag for a new track) — reset it here so looping keeps working
            // past the first repeat instead of only firing once.
            _hasHandledEndOfStream = false;
            return;
        }

        if (RepeatMode == RepeatMode.RepeatAll && !_queue.CanGoNext)
        {
            var track = _queue.MoveToStart();
            if (track is not null)
            {
                await LoadTrackAsync(track);
            }

            return;
        }

        var next = _queue.MoveNext();
        if (next is not null)
        {
            await LoadTrackAsync(next);
        }
        else
        {
            // End of the queue with Repeat off — actually stop the engine (it doesn't do this on
            // its own; Position just keeps climbing past Duration, see the note above) and disable
            // Play/Pause so nothing tries to resume a track that has already finished.
            _audioEngine.Pause();
            IsPlaying = false;
            TransportState = TransportState.Finished;
        }
    }
}
