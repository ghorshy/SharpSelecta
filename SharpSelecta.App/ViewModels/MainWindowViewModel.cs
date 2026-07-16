using Microsoft.Extensions.Logging;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public PlaybackControlsViewModel PlaybackControls { get; }

    public LibraryViewModel Library { get; }

    public QueueViewModel Queue { get; }

    public MainWindowViewModel(
        IAudioEngine audioEngine,
        IFilePickerService filePickerService,
        ILogger<PlaybackControlsViewModel> playbackControlsLogger,
        ILogger<LibraryViewModel> libraryLogger)
    {
        var queue = new PlaybackQueue();
        PlaybackControls = new PlaybackControlsViewModel(audioEngine, queue, playbackControlsLogger);
        Library = new LibraryViewModel(filePickerService, PlaybackControls, queue, libraryLogger);
        Queue = new QueueViewModel(queue);
    }
}
