using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
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

    // Reference resolution for the waveform display - WaveformSliderView downsamples this
    // client-side to however many constant-width bars actually fit its current width.
    private const int WaveformMasterPointCount = 2000;

    private readonly IAudioEngine _audioEngine;
    private readonly PlaybackQueue _queue;
    private readonly ILogger<PlaybackControlsViewModel> _logger;
    private readonly string _settingsFilePath;
    private bool _isSyncingFromEngine;
    private bool _hasHandledEndOfStream;
    private int _waveformLoadGeneration;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    public partial TransportState TransportState { get; set; } = TransportState.NoTrack;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseLabel))]
    public partial bool IsPlaying { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionDisplay))]
    [NotifyPropertyChangedFor(nameof(DurationDisplay))]
    public partial double PositionSeconds { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationDisplay))]
    public partial double DurationSeconds { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationDisplay))]
    private partial bool ShowRemainingTime { get; set; }
    [ObservableProperty]
    public partial double Volume { get; set; } = 1.0;
    [ObservableProperty]
    public partial VolumeCurve VolumeCurve { get; set; } = VolumeCurve.Linear;
    [ObservableProperty]
    public partial int SeekStepSeconds { get; set; } = 5;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SeekBackwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(SeekForwardCommand))]
    public partial bool IsArrowKeyNavigationFocused { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayFileName))]
    [NotifyPropertyChangedFor(nameof(DisplayTrackLabel))]
    public partial string? LoadedFileName { get; private set; }
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentTrackTechnicalSummary))]
    [NotifyPropertyChangedFor(nameof(DisplayTrackLabel))]
    public partial Track? CurrentTrack { get; set; }

    public string CurrentTrackTechnicalSummary => CurrentTrack is null ? string.Empty : TrackFormatting.TechnicalSummary(CurrentTrack);

    // "Album artist - Title": a track with many featured artists would otherwise crowd the line, so
    // it's labelled by the album artist, and only falls back to its artists when it has none.
    public string DisplayTrackLabel => TrackFormatting.ArtistTitleLabel(
        string.IsNullOrWhiteSpace(CurrentTrack?.AlbumArtist) ? CurrentTrack?.Artist : CurrentTrack.AlbumArtist,
        DisplayFileName);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArtworkBytes))]
    public partial byte[]? CurrentTrackArtworkBytes { get; set; }

    public byte[]? ArtworkBytes => CurrentTrackArtworkBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RepeatModeLabel))]
    public partial RepeatMode RepeatMode { get; set; } = RepeatMode.Off;

    [ObservableProperty]
    public partial IReadOnlyList<float> WaveformPeaks { get; private set; } = [];

    // True from the moment extraction starts until it (successfully or not) finishes -
    // WaveformSliderView shows a flat placeholder line instead of a blank control while this is set.
    [ObservableProperty]
    public partial bool IsWaveformLoading { get; private set; }

    // Fire-and-forget from the UI's perspective (see LoadTrackCoreAsync) - exposed so tests
    // can await it instead of racing the background extraction.
    public Task WaveformLoadTask { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    public partial bool UseWaveformSlider { get; set; }

    public PlaybackControlsViewModel(IAudioEngine audioEngine, PlaybackQueue queue, string settingsFilePath, ILogger<PlaybackControlsViewModel> logger)
    {
        _audioEngine = audioEngine;
        _queue = queue;
        _settingsFilePath = settingsFilePath;
        _logger = logger;

        ((INotifyCollectionChanged)_queue.Entries).CollectionChanged += (_, _) =>
        {
            RefreshNavigationCommands();
            TopUpAutoDj();
        };
        _queue.CurrentIndexChanged += (_, _) =>
        {
            RefreshNavigationCommands();
            OnPropertyChanged(nameof(QueueCurrentIndex));
            TopUpAutoDj();
        };

        IsAutoDjEnabled = SettingsStore.LoadAutoDjEnabled(settingsFilePath);
        _autoDjSettingLoaded = true;
    }

    private bool _autoDjSettingLoaded;
    private bool _isToppingUpAutoDj;

    // Auto DJ keeps the queue topped up with random tracks so the music doesn't run out.
    [ObservableProperty]
    public partial bool IsAutoDjEnabled { get; set; }

    // Where Auto DJ draws from (the playlist playback was started from, else the library). Asked each
    // time rather than copied, so edits to a playlist are picked up.
    public Func<IReadOnlyList<Track>>? AutoDjPool { get; set; }

    partial void OnIsAutoDjEnabledChanged(bool value)
    {
        if (!_autoDjSettingLoaded)
            return;

        SettingsStore.SaveAutoDjEnabled(_settingsFilePath, value);
        TopUpAutoDj();
    }

    [RelayCommand]
    private void ToggleAutoDj() => IsAutoDjEnabled = !IsAutoDjEnabled;

    // Keeps AutoDj.Lookahead tracks queued after the current one. Does nothing until something is playing.
    public void TopUpAutoDj()
    {
        if (!IsAutoDjEnabled || AutoDjPool is null || _queue.CurrentIndex < 0 || _isToppingUpAutoDj)
            return;

        var entries = _queue.Entries;
        var current = _queue.CurrentIndex;
        var needed = AutoDj.Lookahead - (entries.Count - 1 - current);
        if (needed <= 0)
            return;

        _isToppingUpAutoDj = true;
        try
        {
            var played = entries.Take(current).Select(e => e.Track.FilePath).ToHashSet();
            var inUse = entries.Skip(current).Select(e => e.Track.FilePath).ToHashSet();
            foreach (var track in AutoDj.Pick(AutoDjPool(), played, inUse, needed, Random.Shared))
            {
                _queue.AddAutoDjEntry(track);
            }
        }
        finally
        {
            _isToppingUpAutoDj = false;
        }
    }

    // With Auto DJ on, "Clear" drops its upcoming picks (and they're re-rolled) but keeps what was queued by hand.
    public void ClearAutoDjEntries() => _queue.ClearAutoDjTail();

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

    // Called after a track's tags/cover were edited: the queue and the now-playing display keep
    // the Track they were handed, so they need the re-read one.
    public async Task RefreshTrackMetadataAsync(Track updated)
    {
        _queue.ReplaceTrack(updated);

        if (CurrentTrack?.FilePath != updated.FilePath)
            return;

        CurrentTrack = updated;
        LoadedFileName = updated.DisplayName;
        CurrentTrackArtworkBytes = await Task.Run(() => MusicLibraryScanner.LoadArtwork(updated.FilePath));
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

    // Throws away the current queue (including whatever is playing) and starts the given tracks, in order.
    public async Task ReplaceQueueAndPlayAsync(IReadOnlyList<Track> tracks)
    {
        if (tracks.Count == 0)
            return;

        _queue.Restore(tracks.Select(track => new QueueEntry(track, QueueEntrySource.Manual)).ToList(), 0);
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

            // Off the critical path deliberately - a big/HQ file's decoder pass here must never
            // delay playback start. WaveformPeaks is cleared immediately so a fast track switch
            // doesn't show the previous track's bars, and the generation check drops a stale
            // result if the user has already moved on to another track by the time this finishes.
            WaveformPeaks = [];
            IsWaveformLoading = true;
            WaveformLoadTask = LoadWaveformPeaksAsync(track, ++_waveformLoadGeneration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load {FilePath}", track.FilePath);
            StatusMessage = Strings.FailedToLoadFile(ex.Message);
        }
    }

    private async Task LoadWaveformPeaksAsync(Track track, int generation)
    {
        try
        {
            var peaks = await Task.Run(() =>
            {
                var cached = LibraryIndexStore.TryGetWaveformPeaks(_settingsFilePath, track.FilePath);
                if (cached is not null)
                {
                    return cached;
                }

                var extracted = _audioEngine.GetWaveformPeaks(WaveformMasterPointCount);
                LibraryIndexStore.SaveWaveformPeaks(_settingsFilePath, track.FilePath, extracted);
                return extracted;
            });

            if (generation == _waveformLoadGeneration)
            {
                WaveformPeaks = peaks;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract waveform peaks for {FilePath}", track.FilePath);
        }
        finally
        {
            if (generation == _waveformLoadGeneration)
            {
                IsWaveformLoading = false;
            }
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
