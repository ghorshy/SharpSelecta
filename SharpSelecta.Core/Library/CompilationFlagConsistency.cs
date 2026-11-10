namespace SharpSelecta.Core.Library;

public sealed record InconsistentCompilationAlbum(
    string Album,
    string AlbumArtist,
    IReadOnlyList<Track> Flagged,
    IReadOnlyList<Track> Unflagged);

// Without an album artist, a partly flagged album is an iTunes-style compilation and is left alone.
// isFlagged may run concurrently; the albums come back unordered.
public static class CompilationFlagConsistency
{
    public static IReadOnlyList<InconsistentCompilationAlbum> FindInconsistent(IEnumerable<Track> tracks, Func<string, bool> isFlagged) =>
    [
        .. tracks
            .GroupBy(AlbumKey.Of)
            .Where(g => g.Key.AlbumArtist.Length > 0)
            .AsParallel()
            .Select(g =>
            {
                var flagged = g.Where(t => isFlagged(t.FilePath)).ToList();
                return new InconsistentCompilationAlbum(g.Key.Album, g.Key.AlbumArtist, flagged, [.. g.Except(flagged)]);
            })
            .Where(a => a.Flagged.Count > 0 && a.Unflagged.Count > 0),
    ];
}
