using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

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
    public async Task PlayPauseCommand_DisabledUntilSomethingIsLoaded()
    {
        var vm = CreateViewModel(out _, out var queue);
        await Assert.That(vm.PlayPauseCommand.CanExecute(null)).IsFalse();

        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));

        await Assert.That(vm.PlayPauseCommand.CanExecute(null)).IsTrue();
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

    [Test]
    public async Task ToggleRepeatModeCommand_CyclesOffThenAllThenOneThenBackToOff()
    {
        var vm = CreateViewModel(out _, out _);
        await Assert.That(vm.RepeatMode).IsEqualTo(RepeatMode.Off);

        vm.ToggleRepeatModeCommand.Execute(null);
        await Assert.That(vm.RepeatMode).IsEqualTo(RepeatMode.RepeatAll);

        vm.ToggleRepeatModeCommand.Execute(null);
        await Assert.That(vm.RepeatMode).IsEqualTo(RepeatMode.RepeatOne);

        vm.ToggleRepeatModeCommand.Execute(null);
        await Assert.That(vm.RepeatMode).IsEqualTo(RepeatMode.Off);
    }

    [Test]
    public async Task RefreshPositionAsync_WhenTrackEndsWithRepeatOne_RestartsTheSameTrack()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        audioEngine.ClearReceivedCalls();
        vm.ToggleRepeatModeCommand.Execute(null); // Off -> RepeatAll
        vm.ToggleRepeatModeCommand.Execute(null); // RepeatAll -> RepeatOne
        audioEngine.Duration.Returns(180.0);
        audioEngine.Position.Returns(200.0);

        await vm.RefreshPositionAsync();

        audioEngine.DidNotReceive().Load(Arg.Any<string>());
        audioEngine.Received(1).Seek(0);
        audioEngine.Received(1).Play();
        await Assert.That(vm.IsPlaying).IsTrue();
    }

    [Test]
    public async Task RefreshPositionAsync_WithRepeatOne_KeepsLoopingPastTheFirstRestart()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        vm.ToggleRepeatModeCommand.Execute(null); // Off -> RepeatAll
        vm.ToggleRepeatModeCommand.Execute(null); // RepeatAll -> RepeatOne
        audioEngine.Duration.Returns(180.0);
        audioEngine.Position.Returns(200.0);

        await vm.RefreshPositionAsync(); // first time reaching the end
        audioEngine.Position.Returns(0.0); // simulates playback having restarted and progressed a bit
        await vm.RefreshPositionAsync(); // should NOT re-trigger while position is back under duration
        audioEngine.Position.Returns(200.0); // reaches the end again
        audioEngine.ClearReceivedCalls();

        await vm.RefreshPositionAsync();

        audioEngine.Received(1).Seek(0);
        audioEngine.Received(1).Play();
    }

    [Test]
    public async Task RefreshPositionAsync_WhenTrackEndsWithRepeatAllAtEndOfQueue_WrapsToStart()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddToQueue(new Track("/music/b.mp3", "b.mp3"));
        await vm.NextTrackCommand.ExecuteAsync(null); // now at b.mp3, end of queue
        vm.ToggleRepeatModeCommand.Execute(null); // Off -> RepeatAll
        // The freshly (re)loaded track isn't itself at its end — only the first check should
        // report end-of-stream, or the fire-and-forget refresh after loading it would cascade.
        audioEngine.Duration.Returns(180.0);
        audioEngine.Position.Returns(200.0, 0.0);

        await vm.RefreshPositionAsync();

        audioEngine.Received(1).Load("/music/a.mp3");
        await Assert.That(vm.LoadedFileName).IsEqualTo("a.mp3");
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task RefreshPositionAsync_WhenTrackEndsWithRepeatOff_AdvancesToNextQueuedTrack()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddToQueue(new Track("/music/b.mp3", "b.mp3"));
        audioEngine.Duration.Returns(180.0);
        audioEngine.Position.Returns(200.0, 0.0);

        await vm.RefreshPositionAsync();

        audioEngine.Received(1).Load("/music/b.mp3");
        await Assert.That(vm.LoadedFileName).IsEqualTo("b.mp3");
    }

    [Test]
    public async Task RefreshPositionAsync_WhenTrackEndsWithRepeatOffAndNothingQueued_StopsPlaying()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        vm.PlayPauseCommand.Execute(null);
        audioEngine.Duration.Returns(180.0);
        audioEngine.Position.Returns(200.0);

        await vm.RefreshPositionAsync();

        audioEngine.DidNotReceive().Load(Arg.Any<string>());
        await Assert.That(vm.IsPlaying).IsFalse();
    }

    [Test]
    public async Task RefreshPositionAsync_OnlyReactsOnceToEndOfStreamUntilNextTrackLoads()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddToQueue(new Track("/music/b.mp3", "b.mp3"));
        audioEngine.Duration.Returns(180.0);
        audioEngine.Position.Returns(200.0, 0.0, 0.0);

        await vm.RefreshPositionAsync();
        audioEngine.ClearReceivedCalls();
        await vm.RefreshPositionAsync();

        audioEngine.DidNotReceive().Load(Arg.Any<string>());
    }

    [Test]
    public async Task PositionDisplay_FormatsSecondsAsMinutesColonSeconds()
    {
        var vm = CreateViewModel(out var audioEngine, out _);
        audioEngine.Position.Returns(65.0);

        await vm.RefreshPositionAsync();

        await Assert.That(vm.PositionDisplay).IsEqualTo("1:05");
    }

    [Test]
    public async Task DurationDisplay_ByDefault_ShowsTotalDuration()
    {
        var vm = CreateViewModel(out var audioEngine, out _);
        audioEngine.Duration.Returns(185.0);

        await vm.RefreshPositionAsync();

        await Assert.That(vm.DurationDisplay).IsEqualTo("3:05");
    }

    [Test]
    public async Task ToggleDurationDisplayCommand_SwitchesToRemainingTime_AndBackAgain()
    {
        var vm = CreateViewModel(out var audioEngine, out _);
        audioEngine.Position.Returns(60.0);
        audioEngine.Duration.Returns(185.0);
        await vm.RefreshPositionAsync();

        vm.ToggleDurationDisplayCommand.Execute(null);
        await Assert.That(vm.DurationDisplay).IsEqualTo("-2:05");

        vm.ToggleDurationDisplayCommand.Execute(null);
        await Assert.That(vm.DurationDisplay).IsEqualTo("3:05");
    }

    [Test]
    public async Task DurationDisplay_ShowsHoursOnceTrackIsAnHourOrLonger()
    {
        var vm = CreateViewModel(out var audioEngine, out _);
        audioEngine.Duration.Returns(3725.0); // 1h 02m 05s

        await vm.RefreshPositionAsync();

        await Assert.That(vm.DurationDisplay).IsEqualTo("1:02:05");
    }
}
