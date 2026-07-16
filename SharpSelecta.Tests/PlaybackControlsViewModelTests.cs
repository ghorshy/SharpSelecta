using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class PlaybackControlsViewModelTests
{
    private static PlaybackControlsViewModel CreateViewModel(out IAudioEngine audioEngine, out PlaybackQueue queue)
    {
        audioEngine = Substitute.For<IAudioEngine>();
        queue = new PlaybackQueue();
        return new PlaybackControlsViewModel(audioEngine, queue, NullLogger<PlaybackControlsViewModel>.Instance);
    }

    [Test]
    public async Task PlayPauseCommand_WhenNotPlaying_PlaysAndUpdatesState()
    {
        var vm = CreateViewModel(out var audioEngine, out _);

        vm.PlayPauseCommand.Execute(null);

        audioEngine.Received(1).Play();
        await Assert.That(vm.IsPlaying).IsTrue();
        await Assert.That(vm.PlayPauseLabel).IsEqualTo("Pause");
    }

    [Test]
    public async Task PlayPauseCommand_WhenPlaying_PausesAndUpdatesState()
    {
        var vm = CreateViewModel(out var audioEngine, out _);
        vm.PlayPauseCommand.Execute(null);

        vm.PlayPauseCommand.Execute(null);

        audioEngine.Received(1).Pause();
        await Assert.That(vm.IsPlaying).IsFalse();
        await Assert.That(vm.PlayPauseLabel).IsEqualTo("Play");
    }

    [Test]
    public async Task SettingVolume_ForwardsToEngine()
    {
        var vm = CreateViewModel(out var audioEngine, out _);

        vm.Volume = 0.4;

        audioEngine.Received(1).Volume = 0.4f;
        await Assert.That(vm.Volume).IsEqualTo(0.4);
    }

    [Test]
    public async Task SettingPositionSeconds_SeeksTheEngine()
    {
        var vm = CreateViewModel(out var audioEngine, out _);

        vm.PositionSeconds = 42.0;

        audioEngine.Received(1).Seek(42.0);
    }

    [Test]
    public async Task RefreshPosition_UpdatesFromEngineWithoutSeeking()
    {
        var vm = CreateViewModel(out var audioEngine, out _);
        audioEngine.Position.Returns(30.0);
        audioEngine.Duration.Returns(180.0);

        vm.RefreshPosition();

        await Assert.That(vm.PositionSeconds).IsEqualTo(30.0);
        await Assert.That(vm.DurationSeconds).IsEqualTo(180.0);
        audioEngine.DidNotReceive().Seek(Arg.Any<double>());
    }

    [Test]
    public async Task NextTrackCommand_DisabledWhenQueueEmpty_EnabledOnceSomethingIsQueued()
    {
        var vm = CreateViewModel(out _, out var queue);
        await Assert.That(vm.NextTrackCommand.CanExecute(null)).IsFalse();

        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddToQueue(new Track("/music/b.mp3", "b.mp3"));

        await Assert.That(vm.NextTrackCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task NextTrackCommand_AdvancesQueueAndLoadsIntoEngine_WithoutDroppingHistory()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddToQueue(new Track("/music/b.mp3", "b.mp3"));

        await vm.NextTrackCommand.ExecuteAsync(null);

        audioEngine.Received(1).Load("/music/b.mp3");
        await Assert.That(vm.LoadedFileName).IsEqualTo("b.mp3");
        await Assert.That(vm.IsPlaying).IsTrue();
        await Assert.That(queue.Entries.Count).IsEqualTo(2);
        await Assert.That(vm.PreviousTrackCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task PreviousTrackCommand_DisabledUntilSomethingIsPlaying()
    {
        var vm = CreateViewModel(out _, out var queue);
        await Assert.That(vm.PreviousTrackCommand.CanExecute(null)).IsFalse();

        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));

        await Assert.That(vm.PreviousTrackCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task PreviousTrackCommand_WithinThreshold_GoesBackAndReloadsThePriorTrack()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddToQueue(new Track("/music/b.mp3", "b.mp3"));
        await vm.NextTrackCommand.ExecuteAsync(null);
        vm.PositionSeconds = 1.0;
        audioEngine.ClearReceivedCalls();

        await vm.PreviousTrackCommand.ExecuteAsync(null);

        audioEngine.Received(1).Load("/music/a.mp3");
        await Assert.That(vm.LoadedFileName).IsEqualTo("a.mp3");
        await Assert.That(queue.Entries.Count).IsEqualTo(2);
        await Assert.That(vm.NextTrackCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task PreviousTrackCommand_PastThreshold_RestartsCurrentTrackInsteadOfGoingBack()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddToQueue(new Track("/music/b.mp3", "b.mp3"));
        await vm.NextTrackCommand.ExecuteAsync(null);
        vm.PositionSeconds = 10.0;
        audioEngine.ClearReceivedCalls();

        await vm.PreviousTrackCommand.ExecuteAsync(null);

        audioEngine.DidNotReceive().Load(Arg.Any<string>());
        audioEngine.Received(1).Seek(0);
        await Assert.That(vm.PositionSeconds).IsEqualTo(0.0);
        await Assert.That(vm.LoadedFileName).IsEqualTo("b.mp3");
    }

    [Test]
    public async Task PreviousTrackCommand_AtStartOfHistory_RestartsEvenWithinThreshold()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        vm.PositionSeconds = 1.0;
        audioEngine.ClearReceivedCalls();

        await vm.PreviousTrackCommand.ExecuteAsync(null);

        audioEngine.DidNotReceive().Load(Arg.Any<string>());
        audioEngine.Received(1).Seek(0);
        await Assert.That(vm.PositionSeconds).IsEqualTo(0.0);
    }

    [Test]
    public async Task PlayNowAsync_JoinsQueueAndLoadsIntoEngine()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        var track = new Track("/music/song.mp3", "song.mp3");

        await vm.PlayNowAsync(track);

        audioEngine.Received(1).Load("/music/song.mp3");
        await Assert.That(vm.LoadedFileName).IsEqualTo("song.mp3");
        await Assert.That(vm.IsPlaying).IsTrue();
        await Assert.That(queue.Entries.Count).IsEqualTo(1);
    }

    [Test]
    public async Task PlayNowAsync_WhenEngineThrows_SetsStatusMessageInsteadOfCrashing()
    {
        var vm = CreateViewModel(out var audioEngine, out _);
        var track = new Track("/music/broken.mp4", "broken.mp4");
        audioEngine.When(e => e.Load("/music/broken.mp4"))
            .Throw(new InvalidOperationException("no audio stream found"));

        await vm.PlayNowAsync(track);

        await Assert.That(vm.StatusMessage).IsNotNull();
        await Assert.That(vm.IsPlaying).IsFalse();
    }
}
