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
    public async Task Clear_WithAutoDjOn_DropsOnlyItsUpcomingPicks_AndKeepsWhatWasQueuedByHand()
    {
        var vm = CreateViewModel(out _, out var queue);
        queue.PlayNow(new Track("/music/current.mp3", "current.mp3"));
        queue.AddToQueue(new Track("/music/manual.mp3", "manual.mp3"));
        queue.AddAutoDjEntry(new Track("/music/auto1.mp3", "auto1.mp3"));
        queue.AddAutoDjEntry(new Track("/music/auto2.mp3", "auto2.mp3"));
        vm.ToggleAutoDjCommand.Execute(null);

        await Assert.That(vm.ClearQueueCommand.CanExecute(null)).IsTrue();
        vm.ClearQueueCommand.Execute(null);

        await Assert.That(queue.Entries.Select(e => e.Track.DisplayName)).IsEquivalentTo(["current.mp3", "manual.mp3"]);
        await Assert.That(vm.ClearQueueCommand.CanExecute(null)).IsFalse(); // nothing of Auto DJ's left to clear
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
}
