using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class AutoDjTests
{
    private static Track T(string name) => new($"/music/{name}.mp3", name);

    private static IReadOnlyList<Track> Pool(params string[] names) => names.Select(T).ToList();

    private static IReadOnlySet<string> Paths(params string[] names) => names.Select(n => T(n).FilePath).ToHashSet();

    [Test]
    public async Task Pick_ReturnsTheRequestedNumberOfDistinctPoolTracks()
    {
        var picks = AutoDj.Pick(Pool("a", "b", "c", "d", "e", "f"), Paths(), Paths(), 4, new Random(1));

        await Assert.That(picks.Count).IsEqualTo(4);
        await Assert.That(picks.Select(t => t.FilePath).Distinct().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task Pick_NeverPicksWhatIsPlayingOrAlreadyQueued()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var picks = AutoDj.Pick(Pool("a", "b", "c", "d", "e", "f"), Paths(), Paths("a", "b"), 4, new Random(seed));

            await Assert.That(picks.Select(t => t.FilePath)).IsEquivalentTo(Paths("c", "d", "e", "f"));
        }
    }

    [Test]
    public async Task Pick_PrefersTracksThatHaveNotPlayedYet()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var picks = AutoDj.Pick(Pool("a", "b", "c", "d", "e"), Paths("a", "b", "c"), Paths("d"), 1, new Random(seed));

            await Assert.That(picks.Single().FilePath).IsEqualTo(T("e").FilePath);
        }
    }

    [Test]
    public async Task Pick_WhenEveryTrackHasPlayed_StartsTheCycleOver_ButNotWithWhatIsInUse()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var picks = AutoDj.Pick(Pool("a", "b", "c"), Paths("a", "b"), Paths("c"), 2, new Random(seed));

            await Assert.That(picks.Select(t => t.FilePath)).IsEquivalentTo(Paths("a", "b"));
        }
    }

    [Test]
    public async Task Pick_UsesUnplayedTracksFirstThenRecyclesPlayedOnesToFillTheCount()
    {
        var picks = AutoDj.Pick(Pool("a", "b", "c", "d"), Paths("a", "b"), Paths("d"), 2, new Random(3));

        // "c" is the only unplayed one that's free, so it's always in; the other comes from the played ones.
        await Assert.That(picks.Select(t => t.FilePath)).Contains(T("c").FilePath);
        await Assert.That(picks.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Pick_FromATinyPool_ReturnsOnlyWhatIsAvailable()
    {
        var picks = AutoDj.Pick(Pool("a", "b"), Paths(), Paths("a"), 5, new Random(1));

        await Assert.That(picks.Select(t => t.FilePath)).IsEquivalentTo(Paths("b"));
    }

    [Test]
    public async Task Pick_WithAnEmptyPoolOrNothingWanted_ReturnsNothing()
    {
        await Assert.That(AutoDj.Pick([], Paths(), Paths(), 5, new Random(1))).IsEmpty();
        await Assert.That(AutoDj.Pick(Pool("a"), Paths(), Paths(), 0, new Random(1))).IsEmpty();
    }

    [Test]
    public async Task Pick_TreatsTheSameFileInThePoolTwiceAsOneTrack()
    {
        var picks = AutoDj.Pick([T("a"), T("a"), T("b")], Paths(), Paths(), 5, new Random(1));

        await Assert.That(picks.Select(t => t.FilePath)).IsEquivalentTo(Paths("a", "b"));
    }

    [Test]
    public async Task ClearAutoDjTail_RemovesOnlyUpcomingAutoDjEntries_KeepingManualAndHistory()
    {
        var queue = new PlaybackQueue();
        queue.AddAutoDjEntry(T("history-auto"));
        queue.AddToQueue(T("current"));
        queue.JumpTo(1);
        queue.AddToQueue(T("manual"));
        queue.AddAutoDjEntry(T("auto1"));
        queue.AddAutoDjEntry(T("auto2"));

        queue.ClearAutoDjTail();

        await Assert.That(queue.Entries.Select(e => e.Track.DisplayName)).IsEquivalentTo(["history-auto", "current", "manual"]);
        await Assert.That(queue.CurrentIndex).IsEqualTo(1);
    }
}
