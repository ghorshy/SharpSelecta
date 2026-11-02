using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.Integrations.AlbumArt;

// Apple Music's covers, through the keyless iTunes Search API (about 20 requests a minute).
public sealed class AppleMusicArtProvider(HttpClient http) : IAlbumArtProvider
{
    public string Name => "Apple Music";

    public async Task<AlbumArtCandidate?> FindAsync(AlbumArtQuery query, CancellationToken cancellationToken)
    {
        var term = Uri.EscapeDataString($"{query.Artist} {query.Album}");
        using var document = await AlbumArtHttp.GetJsonAsync(http, $"https://itunes.apple.com/search?term={term}&entity=album&limit=10", cancellationToken);
        if (document is null || !document.RootElement.TryGetProperty("results", out var results))
            return null;

        var hits = results.EnumerateArray()
            .Select(r => new AlbumHit(r.StringOf("collectionName"), r.StringOf("artistName"), r.StringOf("artworkUrl100")))
            .Where(hit => hit.Reference.Length > 0)
            .ToList();

        var best = AlbumMatcher.PickBest(hits, query);
        if (best is null)
            return null;

        // The service serves any size up to the original from the same path - asking for far more than
        // exists returns the original.
        var largest = await AlbumArtHttp.DownloadImageAsync(
            http, best.Reference.Replace("100x100bb", "3000x3000bb"), best.Album, cancellationToken);

        // An original too big to accept (or otherwise refused) shouldn't mean no cover at all.
        return largest ?? await AlbumArtHttp.DownloadImageAsync(
            http, best.Reference.Replace("100x100bb", "1400x1400bb"), best.Album, cancellationToken);
    }
}
