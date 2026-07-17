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
        var vm = CreateViewModel(out _, out _);
        await Assert.That(vm.PlayPauseCommand.CanExecute(null)).IsFalse();

        // Mutating the queue alone isn't enough — Play/Pause reflects TransportState, which only
        // becomes Ready once the track has actually been loaded into the engine.
        await vm.PlayNowAsync(new Track("/music/a.mp3", "a.mp3"));

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
    public async Task PreviousTrackCommand_AfterQueueFinished_ReEnablesPlayPause()
    {
        var vm = CreateViewModel(out var audioEngine, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        audioEngine.Duration.Returns(180.0);
        audioEngine.Position.Returns(200.0); // triggers natural end-of-queue -> _isQueueFinished
        await vm.RefreshPositionAsync();
        await Assert.That(vm.PlayPauseCommand.CanExecute(null)).IsFalse();

        await vm.PreviousTrackCommand.ExecuteAsync(null); // past threshold -> restarts current track

        await Assert.That(vm.PlayPauseCommand.CanExecute(null)).IsTrue();
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
    public async Task PlayNowAsync_SetsCurrentTrack()
    {
        var vm = CreateViewModel(out _, out _);
        var track = new Track("/music/song.mp3", "song.mp3");

        await vm.PlayNowAsync(track);

        await Assert.That(vm.CurrentTrack).IsEqualTo(track);
    }

    [Test]
    public async Task LoadTrackAsync_WithEmbeddedArtwork_SetsCurrentTrackArtwork()
    {
        var vm = CreateViewModel(out _, out _);
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track-with-artwork.mp3");
        var track = new Track(fixturePath, "tagged-track-with-artwork.mp3");

        await vm.LoadTrackAsync(track);

        await Assert.That(vm.CurrentTrackArtworkBytes).IsNotNull();
    }

    [Test]
    public async Task LoadTrackAsync_WithNoEmbeddedArtwork_LeavesCurrentTrackArtworkNull()
    {
        var vm = CreateViewModel(out _, out _);
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3");
        var track = new Track(fixturePath, "tagged-track.mp3");

        await vm.LoadTrackAsync(track);

        await Assert.That(vm.CurrentTrackArtworkBytes).IsNull();
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
        audioEngine.Received(1).Pause();
        await Assert.That(vm.IsPlaying).IsFalse();
        await Assert.That(vm.PlayPauseCommand.CanExecute(null)).IsFalse();
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

    [Test]
    public async Task MoveQueueEntry_ReordersTheUnderlyingQueue()
    {
        var vm = CreateViewModel(out _, out var queue);
        var a = new Track("/music/a.mp3", "a.mp3");
        var b = new Track("/music/b.mp3", "b.mp3");
        var c = new Track("/music/c.mp3", "c.mp3");
        queue.PlayNow(a);
        queue.AddToQueue(b);
        queue.AddToQueue(c);

        vm.MoveQueueEntry(queue.Entries[2], queue.Entries[0]);

        await Assert.That(queue.Entries[0].Track).IsEqualTo(c);
        await Assert.That(queue.Entries[1].Track).IsEqualTo(a);
        await Assert.That(queue.Entries[2].Track).IsEqualTo(b);
    }

    [Test]
    public async Task MoveQueueEntry_WithNoTarget_MovesToTheEndOfTheQueue()
    {
        var vm = CreateViewModel(out _, out var queue);
        var a = new Track("/music/a.mp3", "a.mp3");
        var b = new Track("/music/b.mp3", "b.mp3");
        queue.PlayNow(a);
        queue.AddToQueue(b);

        vm.MoveQueueEntry(queue.Entries[0], null);

        await Assert.That(queue.Entries[0].Track).IsEqualTo(b);
        await Assert.That(queue.Entries[1].Track).IsEqualTo(a);
    }

    // ObservableCollection<T>.Move interprets its target index against the list AFTER the source
    // entry is removed, so dropping an entry further down the queue would otherwise land it one
    // slot past where the drop indicator (a line above the target row) shows.
    [Test]
    public async Task MoveQueueEntry_DownwardDrag_LandsImmediatelyBeforeTheTargetEntry()
    {
        var vm = CreateViewModel(out _, out var queue);
        var a = new Track("/music/a.mp3", "a.mp3");
        var b = new Track("/music/b.mp3", "b.mp3");
        var c = new Track("/music/c.mp3", "c.mp3");
        queue.PlayNow(a);
        queue.AddToQueue(b);
        queue.AddToQueue(c);

        vm.MoveQueueEntry(queue.Entries[0], queue.Entries[2]); // drag a onto c

        await Assert.That(queue.Entries[0].Track).IsEqualTo(b);
        await Assert.That(queue.Entries[1].Track).IsEqualTo(a);
        await Assert.That(queue.Entries[2].Track).IsEqualTo(c);
    }

    // QueueEntry is a record, so IndexOf-by-value would resolve to the FIRST structurally-equal
    // entry when the same track is queued twice — MoveQueueEntry must act on the exact instance
    // it was given instead. Under the old value-equality lookup, both the dragged duplicate and
    // the target here resolve to the same (first) index, so the move would have silently no-op'd.
    [Test]
    public async Task MoveQueueEntry_WithTheSameTrackQueuedTwice_ActsOnTheExactEntryInstance()
    {
        var vm = CreateViewModel(out _, out var queue);
        var a = new Track("/music/a.mp3", "a.mp3");
        var b = new Track("/music/b.mp3", "b.mp3");
        queue.PlayNow(a);
        queue.AddToQueue(b);
        queue.AddToQueue(a); // same track queued again -> structurally equal to Entries[0]
        var currentEntry = queue.Entries[0];
        var duplicateEntry = queue.Entries[2];

        vm.MoveQueueEntry(duplicateEntry, currentEntry);

        await Assert.That(queue.Entries.Count).IsEqualTo(3);
        await Assert.That(ReferenceEquals(queue.Entries[0], duplicateEntry)).IsTrue();
        await Assert.That(ReferenceEquals(queue.Entries[1], currentEntry)).IsTrue();
        await Assert.That(ReferenceEquals(queue.Entries[2].Track, b)).IsTrue();
        await Assert.That(queue.CurrentIndex).IsEqualTo(1);
    }

    [Test]
    public async Task RemoveFromQueue_RemovesTheGivenEntry()
    {
        var vm = CreateViewModel(out _, out var queue);
        var a = new Track("/music/a.mp3", "a.mp3");
        var b = new Track("/music/b.mp3", "b.mp3");
        queue.PlayNow(a);
        queue.AddToQueue(b);

        vm.RemoveFromQueue(queue.Entries[1]);

        await Assert.That(queue.Entries.Count).IsEqualTo(1);
        await Assert.That(queue.Entries[0].Track).IsEqualTo(a);
    }

    // Under the old value-equality lookup, removing the duplicate here would have resolved to the
    // first (currently-playing) occurrence and silently no-op'd instead of removing the row clicked.
    [Test]
    public async Task RemoveFromQueue_WithTheSameTrackQueuedTwice_RemovesTheExactEntryInstance()
    {
        var vm = CreateViewModel(out _, out var queue);
        var a = new Track("/music/a.mp3", "a.mp3");
        var b = new Track("/music/b.mp3", "b.mp3");
        queue.PlayNow(a);
        queue.AddToQueue(b);
        queue.AddToQueue(a); // same track queued again -> structurally equal to the current entry
        var currentEntry = queue.Entries[0];
        var duplicateEntry = queue.Entries[2];

        vm.RemoveFromQueue(duplicateEntry);

        await Assert.That(queue.Entries.Count).IsEqualTo(2);
        await Assert.That(ReferenceEquals(queue.Entries[0], currentEntry)).IsTrue();
        await Assert.That(ReferenceEquals(queue.Entries[1].Track, b)).IsTrue();
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }

    // Under the old value-equality lookup, jumping to the duplicate here would have resolved to
    // index 0 (already current) and done nothing, instead of jumping to the clicked row at index 2.
    [Test]
    public async Task PlayQueueEntryAsync_WithTheSameTrackQueuedTwice_JumpsToTheExactEntryInstance()
    {
        var vm = CreateViewModel(out _, out var queue);
        var a = new Track("/music/a.mp3", "a.mp3");
        var b = new Track("/music/b.mp3", "b.mp3");
        queue.PlayNow(a);
        queue.AddToQueue(b);
        queue.AddToQueue(a); // duplicate, structurally equal to Entries[0]

        await vm.PlayQueueEntryAsync(queue.Entries[2]);

        await Assert.That(queue.CurrentIndex).IsEqualTo(2);
    }

    [Test]
    public async Task RemoveFromQueue_TheCurrentlyPlayingEntry_IsANoOp()
    {
        var vm = CreateViewModel(out _, out var queue);
        var a = new Track("/music/a.mp3", "a.mp3");
        var b = new Track("/music/b.mp3", "b.mp3");
        queue.PlayNow(a);
        queue.AddToQueue(b);

        vm.RemoveFromQueue(queue.Entries[0]);

        await Assert.That(queue.Entries.Count).IsEqualTo(2);
    }
}
