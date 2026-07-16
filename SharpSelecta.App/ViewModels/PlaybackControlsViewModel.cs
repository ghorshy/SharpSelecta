using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.App.ViewModels;

public partial class PlaybackControlsViewModel : ViewModelBase
{
    private readonly IAudioEngine _audioEngine;
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

    public PlaybackControlsViewModel(IAudioEngine audioEngine)
    {
        _audioEngine = audioEngine;
    }

    public string PlayPauseLabel => IsPlaying ? Strings.Pause : Strings.Play;

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

    // Previous/Next are UI scaffolding for the future track queue — disabled until one exists.
    [RelayCommand(CanExecute = nameof(CanNavigateTrack))]
    private void PreviousTrack()
    {
    }

    [RelayCommand(CanExecute = nameof(CanNavigateTrack))]
    private void NextTrack()
    {
    }

    private bool CanNavigateTrack() => false;

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
