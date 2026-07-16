using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class QueueViewModelTests
{
    private static QueueViewModel CreateViewModel(out IAudioEngine audioEngine, out PlaybackQueue queue)
    {
        audioEngine = Substitute.For<IAudioEngine>();
        queue = new PlaybackQueue();
        var playbackControls = new PlaybackControlsViewModel(audioEngine, queue, NullLogger<PlaybackControlsViewModel>.Instance);
        return new QueueViewModel(playbackControls);
    }

    [Test]
    public async Task PlayEntryCommand_JumpsToTheDoubleClickedEntryAndLoadsIntoEngine()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddToQueue(new Track("/music/b.mp3", "b.mp3"));
        queue.AddToQueue(new Track("/music/c.mp3", "c.mp3"));

        await vm.PlayEntryCommand.ExecuteAsync(vm.Entries[2]);

        audioEngine.Received(1).Load("/music/c.mp3");
        await Assert.That(vm.CurrentIndex).IsEqualTo(2);
    }

    [Test]
    public async Task PlayEntryCommand_DoesNotDropAnyOtherQueueEntries()
    {
        var vm = CreateViewModel(out _, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddToQueue(new Track("/music/b.mp3", "b.mp3"));
        queue.AddToQueue(new Track("/music/c.mp3", "c.mp3"));

        await vm.PlayEntryCommand.ExecuteAsync(vm.Entries[0]);

        await Assert.That(vm.Entries.Count).IsEqualTo(3);
    }
}
