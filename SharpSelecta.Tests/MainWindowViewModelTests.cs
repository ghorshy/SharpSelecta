using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(out IAudioEngine audioEngine)
    {
        audioEngine = Substitute.For<IAudioEngine>();
        var filePickerService = Substitute.For<IFilePickerService>();
        return new MainWindowViewModel(
            audioEngine,
            filePickerService,
            NullLogger<PlaybackControlsViewModel>.Instance,
            NullLogger<LibraryViewModel>.Instance);
    }

    [Test]
    public async Task PlayingTrackFromLibrary_UpdatesSharedPlaybackControlsState()
    {
        var vm = CreateViewModel(out var audioEngine);
        var track = new Track("/music/song.mp3", "song.mp3");

        await vm.Library.PlayNowCommand.ExecuteAsync(track);

        audioEngine.Received(1).Load("/music/song.mp3");
        await Assert.That(vm.PlaybackControls.IsPlaying).IsTrue();
        await Assert.That(vm.PlaybackControls.PlayPauseLabel).IsEqualTo("Pause");
    }

    [Test]
    public async Task QueueingFromLibrary_MakesNextTrackCommandConsumeSharedQueue()
    {
        var vm = CreateViewModel(out var audioEngine);
        var track = new Track("/music/song.mp3", "song.mp3");

        vm.Library.AddToQueueCommand.Execute(track);
        await Assert.That(vm.Queue.Entries.Count).IsEqualTo(1);

        await vm.PlaybackControls.NextTrackCommand.ExecuteAsync(null);

        audioEngine.Received(1).Load("/music/song.mp3");
        await Assert.That(vm.Queue.Entries.Count).IsEqualTo(1);
        await Assert.That(vm.Queue.CurrentIndex).IsEqualTo(0);
    }
}
