using SharpSelecta.Core.Library;

namespace SharpSelecta.Core.Playback;

// Picks the tracks Auto DJ adds to the queue: random ones from a pool, favouring tracks that haven't
// played yet this session so nothing repeats until the whole pool has been used.
public static class AutoDj
{
    // How many upcoming tracks the queue is kept at.
    public const int Lookahead = 5;

    // played: tracks already behind the current one in the queue; inUse: the current track and what's
    // queued after it. Neither is picked again while an unplayed track is left; once the pool is used up
    // the played ones come round again (never what's playing or already queued next).
    public static IReadOnlyList<Track> Pick(
        IReadOnlyList<Track> pool, IReadOnlySet<string> played, IReadOnlySet<string> inUse, int count, Random random)
    {
        if (count <= 0)
            return [];

        var picks = Shuffled(pool.Where(t => !inUse.Contains(t.FilePath) && !played.Contains(t.FilePath)), random)
            .Take(count)
            .ToList();
        if (picks.Count == count)
            return picks;

        var chosen = picks.Select(t => t.FilePath).ToHashSet();
        picks.AddRange(Shuffled(pool.Where(t => !inUse.Contains(t.FilePath) && !chosen.Contains(t.FilePath)), random)
            .Take(count - picks.Count));
        return picks;
    }

    private static List<Track> Shuffled(IEnumerable<Track> tracks, Random random)
    {
        var list = tracks.DistinctBy(t => t.FilePath).ToList();
        random.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
        return list;
    }
}
