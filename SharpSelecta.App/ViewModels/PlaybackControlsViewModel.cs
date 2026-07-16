using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseLabel))]
    private bool isPlaying;

    [ObservableProperty]
    private double positionSeconds;

    [ObservableProperty]
    private double durationSeconds;

    [ObservableProperty]
    private double volume = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayFileName))]
    private string? loadedFileName;

    [ObservableProperty]
    private string? statusMessage;

    public PlaybackControlsViewModel(IAudioEngine audioEngine, PlaybackQueue queue, ILogger<PlaybackControlsViewModel> logger)
    {
        _audioEngine = audioEngine;
        _queue = queue;
        _logger = logger;

        // Keeps Previous/Next enabled state in sync as the queue is edited or navigated,
        // whether that happens here or from the Library's context menu.
        _queue.Entries.CollectionChanged += (_, _) => RefreshNavigationCommands();
        _queue.CurrentIndexChanged += (_, _) => RefreshNavigationCommands();
    }

    public string PlayPauseLabel => IsPlaying ? Strings.Pause : Strings.Play;

    public string DisplayFileName => LoadedFileName ?? Strings.NoFileLoaded;

    [RelayCommand]
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

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
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

    private bool CanGoPrevious() => _queue.CurrentIndex >= 0;

    private void RestartCurrentTrack() => PositionSeconds = 0;

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

    private async Task LoadTrackAsync(Track track)
    {
        try
        {
            await Task.Run(() => _audioEngine.Load(track.FilePath));
            LoadedFileName = track.DisplayName;
            StatusMessage = null;
            IsPlaying = false;
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

    public void RefreshPosition()
    {
        _isSyncingFromEngine = true;
        PositionSeconds = _audioEngine.Position;
        DurationSeconds = _audioEngine.Duration;
        _isSyncingFromEngine = false;
    }
}
