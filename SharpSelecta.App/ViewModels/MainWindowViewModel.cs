using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const double DefaultRightColumnWidth = 220;

    private readonly string _settingsFilePath;

    public PlaybackControlsViewModel PlaybackControls { get; }

    public LibraryViewModel Library { get; }

    public QueueViewModel Queue { get; }

    [ObservableProperty]
    private GridLength rightColumnWidth;

    public MainWindowViewModel(
        IAudioEngine audioEngine,
        IFilePickerService filePickerService,
        string librarySettingsFilePath,
        ILogger<PlaybackControlsViewModel> playbackControlsLogger,
        ILogger<LibraryViewModel> libraryLogger,
        ILogger<QueueViewModel> queueLogger)
    {
        _settingsFilePath = librarySettingsFilePath;

        var queue = new PlaybackQueue();
        PlaybackControls = new PlaybackControlsViewModel(audioEngine, queue, playbackControlsLogger);
        Library = new LibraryViewModel(filePickerService, PlaybackControls, librarySettingsFilePath, libraryLogger);
        Queue = new QueueViewModel(PlaybackControls, queueLogger);

        // Assigning the backing field directly (not the generated property) so loading the saved
        // width on startup doesn't immediately re-save the same value it was just loaded from.
        rightColumnWidth = new GridLength(LibrarySettingsStore.LoadRightColumnWidth(_settingsFilePath) ?? DefaultRightColumnWidth);
    }

    public void PersistRightColumnWidth() =>
        LibrarySettingsStore.SaveRightColumnWidth(_settingsFilePath, RightColumnWidth.Value);
}
