using System;
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
        var settingsFilePath = Path.Combine(Path.GetTempPath(), $"sharpselecta-queue-vm-tests-{Guid.NewGuid():N}.json");
        var playbackControls = new PlaybackControlsViewModel(audioEngine, queue, settingsFilePath, NullLogger<PlaybackControlsViewModel>.Instance);
        return new QueueViewModel(playbackControls, NullLogger<QueueViewModel>.Instance);
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

    [Test]
    public async Task ClearQueueCommand_RemovesEveryEntryButTheCurrentOne()
    {
        var vm = CreateViewModel(out _, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddToQueue(new Track("/music/b.mp3", "b.mp3"));
        queue.AddToQueue(new Track("/music/c.mp3", "c.mp3"));

        vm.ClearQueueCommand.Execute(null);

        await Assert.That(vm.Entries.Count).IsEqualTo(1);
        await Assert.That(vm.Entries[0].Title).IsEqualTo("a.mp3");
    }

    [Test]
    public async Task ClearQueueCommand_WithOnlyTheCurrentEntryQueued_CannotExecute()
    {
        var vm = CreateViewModel(out _, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));

        await Assert.That(vm.ClearQueueCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task ClearQueueCommand_WithMoreThanTheCurrentEntryQueued_CanExecute()
    {
        var vm = CreateViewModel(out _, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddToQueue(new Track("/music/b.mp3", "b.mp3"));

        await Assert.That(vm.ClearQueueCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public void ReportDragReorderFailure_DoesNotThrow()
    {
        var vm = CreateViewModel(out _, out _);

        vm.ReportDragReorderFailure(new InvalidOperationException("simulated drag failure"));
    }

    [Test]
    public async Task EntryArtist_ShowsSeveralArtistsWithTheDisplaySeparator()
    {
        var vm = CreateViewModel(out _, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3") { Artist = "TRC;Trilla Jermaine Trilloski" });

        await Assert.That(vm.Entries[0].Artist).IsEqualTo("TRC, Trilla Jermaine Trilloski");
    }

    [Test]
    public async Task EntryIsAutoDj_ReflectsWhoQueuedIt()
    {
        var vm = CreateViewModel(out _, out var queue);
        queue.PlayNow(new Track("/music/a.mp3", "a.mp3"));
        queue.AddAutoDjEntry(new Track("/music/b.mp3", "b.mp3"));

        await Assert.That(vm.Entries[0].IsAutoDj).IsFalse();
        await Assert.That(vm.Entries[1].IsAutoDj).IsTrue();
    }

    [Test]
    public async Task ToggleAutoDj_FlipsTheFlagAndNotifies()
    {
        var vm = CreateViewModel(out _, out _);
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.ToggleAutoDjCommand.Execute(null);

        await Assert.That(vm.IsAutoDjEnabled).IsTrue();
        await Assert.That(changes).Contains(nameof(QueueViewModel.IsAutoDjEnabled));
    }

    [Test]
    public async Task Clear_WithAutoDjOff_StillEmptiesEverythingButTheCurrentTrack()
    {
        var vm = CreateViewModel(out _, out var queue);
        queue.PlayNow(new Track("/music/current.mp3", "current.mp3"));
        queue.AddToQueue(new Track("/music/manual.mp3", "manual.mp3"));
        queue.AddAutoDjEntry(new Track("/music/auto.mp3", "auto.mp3"));

        vm.ClearQueueCommand.Execute(null);

        await Assert.That(queue.Entries.Select(e => e.Track.DisplayName)).IsEquivalentTo(["current.mp3"]);
    }

    // --- Auto DJ: the queue view must always mirror the real queue ---

    private static (QueueViewModel Queue, PlaybackControlsViewModel Playback, IReadOnlyList<Track> Pool) CreateAutoDjQueue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sharpselecta-queue-sync-{Guid.NewGuid():N}.json");
        var playback = new PlaybackControlsViewModel(Substitute.For<IAudioEngine>(), new PlaybackQueue(), path, NullLogger<PlaybackControlsViewModel>.Instance);
        var queue = new QueueViewModel(playback, NullLogger<QueueViewModel>.Instance);
        IReadOnlyList<Track> pool = Enumerable.Range(0, 30).Select(i => new Track($"/music/{i:D2}.mp3", $"{i:D2}")).ToList();
        playback.AutoDjPool = () => pool;
        playback.IsAutoDjEnabled = true;
        return (queue, playback, pool);
    }

    private static async Task AssertViewMirrorsQueue(QueueViewModel queue, PlaybackControlsViewModel playback)
    {
        await Assert.That(string.Join(",", queue.Entries.Select(e => e.Entry.Track.FilePath)))
            .IsEqualTo(string.Join(",", playback.QueueEntries.Select(e => e.Track.FilePath)));
        await Assert.That(queue.Entries.Single(e => e.IsCurrent).Entry.Track).IsEqualTo(playback.CurrentTrack);
    }

    [Test]
    public async Task AutoDj_QueueViewStaysInSyncWithTheRealQueue_AcrossEveryKindOfChange()
    {
        var (queue, playback, pool) = CreateAutoDjQueue();

        await playback.PlayNowAsync(pool[0]);
        await AssertViewMirrorsQueue(queue, playback);

        await playback.AddToQueue(pool[20]);
        await playback.AddToQueue(pool[21]);
        await AssertViewMirrorsQueue(queue, playback);

        playback.RemoveFromQueue(playback.QueueEntries[3]);
        await AssertViewMirrorsQueue(queue, playback);

        await playback.NextTrackCommand.ExecuteAsync(null);
        await playback.NextTrackCommand.ExecuteAsync(null);
        await AssertViewMirrorsQueue(queue, playback);

        queue.ClearQueueCommand.Execute(null);
        await AssertViewMirrorsQueue(queue, playback);

        await playback.ReplaceQueueAndPlayAsync([pool[7]]);
        await AssertViewMirrorsQueue(queue, playback);
    }

    [Test]
    public async Task AutoDj_Clear_EmptiesTheQueueExceptTheCurrentTrack_ThenRefillsItWithFreshPicks()
    {
        var (queue, playback, pool) = CreateAutoDjQueue();
        await playback.PlayNowAsync(pool[0]);
        await playback.AddToQueue(pool[25]);
        await playback.NextTrackCommand.ExecuteAsync(null);
        var current = playback.CurrentTrack;
        var before = playback.QueueEntries.Select(e => e.Track.FilePath).ToList();

        queue.ClearQueueCommand.Execute(null);

        // Nothing from before the current track survives, and no hand-queued track either...
        await Assert.That(playback.QueueEntries[0].Track).IsEqualTo(current);
        await Assert.That(playback.QueueCurrentIndex).IsEqualTo(0);
        await Assert.That(playback.QueueEntries.Count).IsEqualTo(1 + AutoDj.Lookahead);
        await Assert.That(playback.QueueEntries.Skip(1).All(e => e.Source == QueueEntrySource.AutoDj)).IsTrue();
        await Assert.That(playback.QueueEntries.Select(e => e.Track.FilePath).Distinct().Count()).IsEqualTo(1 + AutoDj.Lookahead);
        await Assert.That(before.Count).IsGreaterThan(1 + AutoDj.Lookahead - 1);
        await AssertViewMirrorsQueue(queue, playback);
    }

    [Test]
    public async Task AutoDj_RemovingAnUpcomingTrack_RefillsTheQueueBackToTheLookahead_InSync()
    {
        var (queue, playback, pool) = CreateAutoDjQueue();
        await playback.PlayNowAsync(pool[0]);

        playback.RemoveFromQueue(playback.QueueEntries[2]);

        await Assert.That(playback.QueueEntries.Count).IsEqualTo(1 + AutoDj.Lookahead);
        await AssertViewMirrorsQueue(queue, playback);
    }
}
