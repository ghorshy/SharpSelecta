using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Formatting;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.App.ViewModels;

public partial class PlaybackControlsViewModel : ViewModelBase, IArtworkPreview
{
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
    private VolumeCurve volumeCurve = VolumeCurve.Linear;

    [ObservableProperty]
    private int seekStepSeconds = 5;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SeekBackwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(SeekForwardCommand))]
    private bool isArrowKeyNavigationFocused;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayFileName))]
    [NotifyPropertyChangedFor(nameof(DisplayTrackLabel))]
    private string? loadedFileName;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentTrackTechnicalSummary))]
    [NotifyPropertyChangedFor(nameof(DisplayTrackLabel))]
    private Track? currentTrack;

    public string CurrentTrackTechnicalSummary => CurrentTrack is null ? string.Empty : TrackFormatting.TechnicalSummary(CurrentTrack);

    public string DisplayTrackLabel => TrackFormatting.ArtistTitleLabel(CurrentTrack?.Artist, DisplayFileName);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArtworkBytes))]
    private byte[]? currentTrackArtworkBytes;

    public byte[]? ArtworkBytes => CurrentTrackArtworkBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RepeatModeLabel))]
    private RepeatMode repeatMode = RepeatMode.Off;

    public PlaybackControlsViewModel(IAudioEngine audioEngine, PlaybackQueue queue, ILogger<PlaybackControlsViewModel> logger)
    {
        _audioEngine = audioEngine;
        _queue = queue;
        _logger = logger;

        ((INotifyCollectionChanged)_queue.Entries).CollectionChanged += (_, _) => RefreshNavigationCommands();
        _queue.CurrentIndexChanged += (_, _) =>
        {
            RefreshNavigationCommands();
            OnPropertyChanged(nameof(QueueCurrentIndex));
        };
    }

    public ReadOnlyObservableCollection<QueueEntry> QueueEntries => _queue.Entries;

    public int QueueCurrentIndex => _queue.CurrentIndex;

    public async Task PlayNext(Track track)
    {
        _queue.PlayNext(track);
        await ResumeIfQueueWasFinishedAsync();
    }

    public async Task PlayNext(IReadOnlyList<Track> tracks)
    {
        _queue.PlayNext(tracks);
        await ResumeIfQueueWasFinishedAsync();
    }

    public async Task AddToQueue(Track track)
    {
        _queue.AddToQueue(track);
        await ResumeIfQueueWasFinishedAsync();
    }

    public async Task AddToQueue(IReadOnlyList<Track> tracks)
    {
        _queue.AddToQueue(tracks);
        await ResumeIfQueueWasFinishedAsync();
    }

    private async Task ResumeIfQueueWasFinishedAsync()
    {
        if (TransportState != TransportState.Finished)
            return;

        var next = _queue.MoveNext();
        if (next is not null)
        {
            await LoadTrackAsync(next);
        }
    }

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

    public void ClearQueueExceptCurrent() => _queue.ClearExceptCurrent();

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

    public string DurationDisplay => ShowRemainingTime
        ? $"-{FormatTime(Math.Max(0, DurationSeconds - PositionSeconds))}"
        : FormatTime(DurationSeconds);

    [RelayCommand]
    private void ToggleDurationDisplay() => ShowRemainingTime = !ShowRemainingTime;

    private static string FormatTime(double totalSeconds) =>
        TrackFormatting.FormatDuration(TimeSpan.FromSeconds(Math.Max(0, totalSeconds)));

    public string RepeatModeLabel => RepeatMode switch
    {
        RepeatMode.RepeatAll => Strings.RepeatAll,
        RepeatMode.RepeatOne => Strings.RepeatOne,
        _ => Strings.RepeatOff,
    };

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
            RestartCurrentTrack();
        }
    }

    private void RestartCurrentTrack()
    {
        PositionSeconds = 0;
        TransportState = TransportState.Ready;
    }

    [RelayCommand(CanExecute = nameof(CanSeek))]
    private void SeekBackward() => Seek(-SeekStepSeconds);

    [RelayCommand(CanExecute = nameof(CanSeek))]
    private void SeekForward() => Seek(SeekStepSeconds);

    private bool CanSeek() => !IsArrowKeyNavigationFocused;

    private void Seek(double deltaSeconds) => PositionSeconds = Math.Clamp(PositionSeconds + deltaSeconds, 0, DurationSeconds);

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

    public async Task PlayNowAsync(Track track)
    {
        _queue.PlayNow(track);
        await LoadTrackAsync(track);
    }

    public async Task PlayNowAsync(IReadOnlyList<Track> tracks)
    {
        if (tracks.Count == 0)
            return;

        _queue.PlayNow(tracks);
        await LoadTrackAsync(tracks[0]);
    }

    public Task LoadTrackAsync(Track track) => LoadTrackCoreAsync(track, autoPlay: true, startPositionSeconds: null);

    public Task RestoreQueueAsync(IReadOnlyList<QueueEntry> entries, int currentIndex, double positionSeconds)
    {
        _queue.Restore(entries, currentIndex);

        var current = _queue.CurrentIndex >= 0 ? _queue.Entries[_queue.CurrentIndex].Track : null;
        return current is null
            ? Task.CompletedTask
            : LoadTrackCoreAsync(current, autoPlay: false, positionSeconds);
    }

    private async Task LoadTrackCoreAsync(Track track, bool autoPlay, double? startPositionSeconds)
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
            if (startPositionSeconds is > 0)
            {
                _audioEngine.Seek(startPositionSeconds.Value);
            }

            RefreshPosition();
            if (autoPlay)
            {
                PlayPauseCommand.Execute(null);
            }
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

    partial void OnVolumeChanged(double value) => ApplyVolumeToEngine();

    partial void OnVolumeCurveChanged(VolumeCurve value) => ApplyVolumeToEngine();

    private void ApplyVolumeToEngine() => _audioEngine.Volume = (float)VolumeScale.ToAmplitude(Volume, VolumeCurve);

    public void RefreshPosition() => _ = RefreshPositionAsync();

    public async Task RefreshPositionAsync()
    {
        _isSyncingFromEngine = true;
        PositionSeconds = _audioEngine.Position;
        DurationSeconds = _audioEngine.Duration;
        _isSyncingFromEngine = false;

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
            _audioEngine.Pause();
            IsPlaying = false;
            TransportState = TransportState.Finished;
        }
    }
}
