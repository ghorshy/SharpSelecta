using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class PlaybackQueueTests
{
    private static readonly Track TrackA = new("/music/a.mp3", "a.mp3");
    private static readonly Track TrackB = new("/music/b.mp3", "b.mp3");
    private static readonly Track TrackC = new("/music/c.mp3", "c.mp3");

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
    public async Task AddToQueue_InsertsBeforeAutoDjTail_NotAtTheVeryEnd()
    {
        var queue = new PlaybackQueue();
        queue.PlayNow(TrackA);
        queue.Entries.Add(new QueueEntry(TrackB, QueueEntrySource.AutoDj));

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
}
