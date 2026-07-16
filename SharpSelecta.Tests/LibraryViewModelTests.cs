using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class LibraryViewModelTests
{
    private static LibraryViewModel CreateViewModel(
        out IAudioEngine audioEngine,
        out IFilePickerService filePickerService,
        out PlaybackControlsViewModel playbackControls,
        out PlaybackQueue queue)
    {
        audioEngine = Substitute.For<IAudioEngine>();
        filePickerService = Substitute.For<IFilePickerService>();
        queue = new PlaybackQueue();
        playbackControls = new PlaybackControlsViewModel(audioEngine, queue, NullLogger<PlaybackControlsViewModel>.Instance);
        return new LibraryViewModel(filePickerService, playbackControls, queue, NullLogger<LibraryViewModel>.Instance);
    }

    [Test]
    public async Task ChooseFolderCommand_WhenFolderSelected_PopulatesTracksFromScan()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _, out _);
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            File.WriteAllBytes(Path.Combine(root.FullName, "song.mp3"), []);
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);

            await vm.ChooseFolderCommand.ExecuteAsync(null);

            await Assert.That(vm.Tracks.Count).IsEqualTo(1);
            await Assert.That(vm.Tracks[0].DisplayName).IsEqualTo("song.mp3");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ChooseFolderCommand_WhenNoFolderSelected_DoesNotTouchTracks()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _, out _);
        filePickerService.PickLibraryFolderAsync().Returns((string?)null);

        await vm.ChooseFolderCommand.ExecuteAsync(null);

        await Assert.That(vm.Tracks).IsEmpty();
    }

    [Test]
    public async Task ChooseFolderCommand_WhenFolderDoesNotExist_SetsStatusMessage()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _, out _);
        filePickerService.PickLibraryFolderAsync().Returns("/no/such/folder");

        await vm.ChooseFolderCommand.ExecuteAsync(null);

        await Assert.That(vm.StatusMessage).IsNotNull();
    }

    [Test]
    public async Task PlayNowCommand_LoadsIntoEngineAndStartsPlaybackViaPlaybackControls()
    {
        var vm = CreateViewModel(out var audioEngine, out _, out var playbackControls, out _);
        var track = new Track("/music/song.mp3", "song.mp3");

        await vm.PlayNowCommand.ExecuteAsync(track);

        audioEngine.Received(1).Load("/music/song.mp3");
        await Assert.That(playbackControls.LoadedFileName).IsEqualTo("song.mp3");
        await Assert.That(playbackControls.IsPlaying).IsTrue();
    }

    [Test]
    public async Task PlayNextCommand_InsertsTrackAtFrontOfQueue()
    {
        var vm = CreateViewModel(out _, out _, out _, out var queue);
        queue.AddToQueue(new Track("/music/existing.mp3", "existing.mp3"));
        var track = new Track("/music/song.mp3", "song.mp3");

        vm.PlayNextCommand.Execute(track);

        await Assert.That(queue.Entries[0].Track).IsEqualTo(track);
    }

    [Test]
    public async Task AddToQueueCommand_AppendsTrackToQueue()
    {
        var vm = CreateViewModel(out _, out _, out _, out var queue);
        var first = new Track("/music/first.mp3", "first.mp3");
        var second = new Track("/music/second.mp3", "second.mp3");

        vm.AddToQueueCommand.Execute(first);
        vm.AddToQueueCommand.Execute(second);

        await Assert.That(queue.Entries[0].Track).IsEqualTo(first);
        await Assert.That(queue.Entries[1].Track).IsEqualTo(second);
    }
}
