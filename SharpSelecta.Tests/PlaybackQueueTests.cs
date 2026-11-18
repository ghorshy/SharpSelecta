using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class PlaybackQueueTests
{
    private static readonly Track TrackA = new(TestPaths.Music("a.mp3"), "a.mp3");
    private static readonly Track TrackB = new(TestPaths.Music("b.mp3"), "b.mp3");
    private static readonly Track TrackC = new(TestPaths.Music("c.mp3"), "c.mp3");

    [Test]
    public async Task PlayNow_InsertsRightAfterCurrentAndBecomesCurrent()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);

        queue.PlayNow(TrackB);

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackB]);
        await Assert.That(queue.CurrentIndex).IsEqualTo(1);
    }

    [Test]
    public async Task PlayNow_DoesNotDiscardAlreadyQueuedUpcomingTracks()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);

        queue.PlayNow(TrackC);

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackC, TrackB]);
        await Assert.That(queue.CurrentIndex).IsEqualTo(1);
    }

    [Test]
    public async Task PlayNext_InsertsRightAfterCurrentWithoutMovingCurrent()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);

        queue.PlayNext(TrackC);

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackC, TrackB]);
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task AddToQueue_AppendsAfterExistingManualUpcomingEntries()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);

        queue.AddToQueue(TrackC);

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackB, TrackC]);
    }

    [Test]
    public async Task PlayNow_WithATrackList_InsertsAllInOrderAndCurrentPointsAtTheFirst()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);

        queue.PlayNow(new[] { TrackB, TrackC });

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackB, TrackC]);
        await Assert.That(queue.CurrentIndex).IsEqualTo(1);
    }

    [Test]
    public async Task PlayNext_WithATrackList_InsertsAllInOrderWithoutMovingCurrent()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);

        queue.PlayNext(new[] { TrackB, TrackC });

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackB, TrackC]);
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task AddToQueue_WithATrackList_AppendsAllInOrder()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddAutoDjEntry(TrackC);

        queue.AddToQueue(new[] { TrackB });

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackB, TrackC]);
    }

    [Test]
    public async Task AddToQueue_InsertsBeforeAutoDjTail_NotAtTheVeryEnd()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddAutoDjEntry(TrackB);

        queue.AddToQueue(TrackC);

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackC, TrackB]);
    }

    [Test]
    public async Task MoveNext_AdvancesWithoutRemovingHistory()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);

        var next = queue.MoveNext();

        await Assert.That(next).IsEqualTo(TrackB);
        await Assert.That(queue.CurrentIndex).IsEqualTo(1);
        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackB]);
    }

    [Test]
    public async Task MoveNext_AtEndOfQueue_ReturnsNullAndDoesNotMove()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);

        var next = queue.MoveNext();

        await Assert.That(next).IsNull();
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task MovePrevious_AfterAdvancing_GoesBackWithoutLosingQueuedTracks()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);
        queue.MoveNext();

        var previous = queue.MovePrevious();

        await Assert.That(previous).IsEqualTo(TrackA);
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackB]);
    }

    [Test]
    public async Task MovePrevious_AtStartOfHistory_ReturnsNullAndDoesNotMove()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);

        var previous = queue.MovePrevious();

        await Assert.That(previous).IsNull();
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task CanGoNextAndCanGoPrevious_ReflectPositionWithinQueue()
    {
        var queue = new PlaybackQueue();
        await Assert.That(queue.CanGoNext).IsFalse();
        await Assert.That(queue.CanGoPrevious).IsFalse();

        queue.PlayNow(TrackA);
        await Assert.That(queue.CanGoPrevious).IsFalse();

        queue.AddToQueue(TrackB);
        await Assert.That(queue.CanGoNext).IsTrue();

        queue.MoveNext();
        await Assert.That(queue.CanGoNext).IsFalse();
        await Assert.That(queue.CanGoPrevious).IsTrue();
    }

    [Test]
    public async Task MoveToStart_WrapsBackToTheFirstEntry()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);
        queue.MoveNext();

        var first = queue.MoveToStart();

        await Assert.That(first).IsEqualTo(TrackA);
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackB]);
    }

    [Test]
    public async Task MoveToStart_OnEmptyQueue_ReturnsNull()
    {
        var queue = new PlaybackQueue();

        var first = queue.MoveToStart();

        await Assert.That(first).IsNull();
    }

    [Test]
    public async Task JumpTo_MovesDirectlyToTheGivenIndexWithoutLosingEntries()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);
        queue.AddToQueue(TrackC);

        var jumped = queue.JumpTo(2);

        await Assert.That(jumped).IsEqualTo(TrackC);
        await Assert.That(queue.CurrentIndex).IsEqualTo(2);
        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackB, TrackC]);
    }

    [Test]
    public async Task JumpTo_WithOutOfRangeIndex_ReturnsNullAndDoesNotMove()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);

        var jumped = queue.JumpTo(5);

        await Assert.That(jumped).IsNull();
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task Move_ReordersEntries()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);
        queue.AddToQueue(TrackC);

        queue.Move(2, 0);

        await Assert.That(queue.Entries[0].Track).IsEqualTo(TrackC);
        await Assert.That(queue.Entries[1].Track).IsEqualTo(TrackA);
        await Assert.That(queue.Entries[2].Track).IsEqualTo(TrackB);
    }

    [Test]
    public async Task Move_OfTheCurrentEntry_KeepsCurrentIndexPointingAtIt()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);
        queue.AddToQueue(TrackC);

        queue.Move(0, 2);

        await Assert.That(queue.CurrentIndex).IsEqualTo(2);
        await Assert.That(queue.Entries[2].Track).IsEqualTo(TrackA);
    }

    [Test]
    public async Task Move_AcrossTheCurrentEntry_ShiftsCurrentIndexToStayOnTheSameEntry()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);
        queue.AddToQueue(TrackC);
        queue.MoveNext();

        queue.Move(2, 0);

        await Assert.That(queue.CurrentIndex).IsEqualTo(2);
        await Assert.That(queue.Entries[2].Track).IsEqualTo(TrackB);
    }

    [Test]
    public async Task RemoveAt_BeforeCurrentIndex_ShiftsCurrentIndexLeft()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);
        queue.MoveNext();

        queue.RemoveAt(0);

        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
        await Assert.That(queue.Entries[0].Track).IsEqualTo(TrackB);
    }

    [Test]
    public async Task RemoveAt_TheCurrentEntry_IsANoOp()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);

        queue.RemoveAt(0);

        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA, TrackB]);
    }

    [Test]
    public async Task RemoveAt_TheOnlyEntry_WhileItIsCurrent_IsANoOp()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);

        queue.RemoveAt(0);

        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
        await Assert.That(queue.Entries.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ClearExceptCurrent_RemovesEveryEntryButTheCurrentOne()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);
        queue.AddToQueue(TrackC);

        queue.ClearExceptCurrent();

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA]);
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task ClearExceptCurrent_WithNoCurrentEntry_EmptiesTheQueue()
    {
        var queue = new PlaybackQueue();

        queue.ClearExceptCurrent();

        await Assert.That(queue.Entries).IsEmpty();
        await Assert.That(queue.CurrentIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task IndexOf_FindsAnEntryByReferenceNotByValue()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);
        queue.AddToQueue(TrackA);

        await Assert.That(queue.IndexOf(queue.Entries[0])).IsEqualTo(0);
        await Assert.That(queue.IndexOf(queue.Entries[2])).IsEqualTo(2);
    }

    [Test]
    public async Task IndexOf_WhenEntryIsNotInTheQueue_ReturnsMinusOne()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);

        await Assert.That(queue.IndexOf(new QueueEntry(TrackB, QueueEntrySource.Manual))).IsEqualTo(-1);
    }

    [Test]
    public async Task Restore_ReplacesEntriesAndSetsCurrentIndex()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);

        QueueEntry[] entries =
        [
            new(TrackA, QueueEntrySource.Manual),
            new(TrackB, QueueEntrySource.AutoDj),
            new(TrackC, QueueEntrySource.Manual),
        ];
        queue.Restore(entries, 1);

        await Assert.That(queue.Entries).IsEquivalentTo(entries);
        await Assert.That(queue.CurrentIndex).IsEqualTo(1);
    }

    [Test]
    public async Task Restore_WithEmptyEntries_SetsCurrentIndexToMinusOne()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);

        queue.Restore([], 0);

        await Assert.That(queue.Entries.Count).IsEqualTo(0);
        await Assert.That(queue.CurrentIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task Restore_WithOutOfRangeCurrentIndex_ClampsToTheLastEntry()
    {
        var queue = new PlaybackQueue();

        queue.Restore([new QueueEntry(TrackA, QueueEntrySource.Manual), new QueueEntry(TrackB, QueueEntrySource.Manual)], 99);

        await Assert.That(queue.CurrentIndex).IsEqualTo(1);
    }

    [Test]
    public async Task ReplaceFile_WithAReReadTrack_SwapsEveryEntryOfThatFile_KeepingSourceAndCurrentIndex()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);
        queue.AddToQueue(TrackA);
        queue.AddAutoDjEntry(TrackA);
        var updated = TrackA with { Title = "Renamed" };

        queue.ReplaceFile(TrackA.FilePath, updated);

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([updated, TrackB, updated, updated]);
        await Assert.That(queue.Entries[3].Source).IsEqualTo(QueueEntrySource.AutoDj);
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task ReplaceFile_ForAFileNotInTheQueue_ChangesNothing()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);

        queue.ReplaceFile(TrackB.FilePath, TrackB with { Title = "Other" });

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackA]);
    }

    [Test]
    public async Task ReplaceFile_SwapsEveryEntryOfTheOldFileForTheReplacement()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddToQueue(TrackB);
        queue.AddToQueue(TrackA);

        queue.ReplaceFile(TrackA.FilePath, TrackC);

        await Assert.That(queue.Entries.Select(e => e.Track)).IsEquivalentTo([TrackC, TrackB, TrackC]);
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }
    private static readonly Track TrackD = new(TestPaths.Music("d.mp3"), "d.mp3");
    private static readonly Track TrackE = new(TestPaths.Music("e.mp3"), "e.mp3");

    private static string Names(PlaybackQueue queue) =>
        string.Join("|", queue.Entries.Select(e => Path.GetFileNameWithoutExtension(e.Track.FilePath)));

    [Test]
    public async Task InsertAt_PutsTheTracksAtThatIndex_InOrder_AsManualEntries()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow([TrackA, TrackB, TrackC]);
        queue.JumpTo(0);

        queue.InsertAt(2, [TrackD, TrackE]);

        await Assert.That(Names(queue)).IsEqualTo("a|b|d|e|c");
        await Assert.That(queue.Entries.Skip(2).Take(2).All(e => e.Source == QueueEntrySource.Manual)).IsTrue();
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task InsertAt_AtOrBeforeTheCurrentEntry_KeepsPlayingTheSameTrack(int index)
    {
        var queue = new PlaybackQueue();
        queue.PlayNow([TrackA, TrackB, TrackC]);
        queue.JumpTo(1);

        queue.InsertAt(index, [TrackD, TrackE]);

        await Assert.That(queue.Entries[queue.CurrentIndex].Track).IsEqualTo(TrackB);
        await Assert.That(queue.CurrentIndex).IsEqualTo(3);
    }

    [Test]
    public async Task InsertAt_RightAfterTheCurrentEntry_LeavesItCurrent()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow([TrackA, TrackB]);
        queue.JumpTo(0);

        queue.InsertAt(1, [TrackD]);

        await Assert.That(Names(queue)).IsEqualTo("a|d|b");
        await Assert.That(queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task InsertAt_PastTheEndOrIntoAnEmptyQueue_Appends()
    {
        var queue = new PlaybackQueue();
        queue.InsertAt(5, [TrackA]);
        await Assert.That(Names(queue)).IsEqualTo("a");
        await Assert.That(queue.CurrentIndex).IsEqualTo(-1);

        queue.InsertAt(99, [TrackB]);
        queue.InsertAt(-3, [TrackC]);

        await Assert.That(Names(queue)).IsEqualTo("c|a|b");
    }

    [Test]
    public async Task InsertAt_AmongAutoDjEntries_StaysWhereItWasDropped()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.AddAutoDjEntry(TrackB);
        queue.AddAutoDjEntry(TrackC);

        queue.InsertAt(2, [TrackD]);

        await Assert.That(Names(queue)).IsEqualTo("a|b|d|c");
        await Assert.That(queue.Entries[2].Source).IsEqualTo(QueueEntrySource.Manual);
    }
}
