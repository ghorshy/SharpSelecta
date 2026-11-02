using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.Integrations.AlbumArt;

// Deezer's covers, through its keyless public API.
public sealed class DeezerArtProvider(HttpClient http) : IAlbumArtProvider
{
    public string Name => "Deezer";

    public async Task<AlbumArtCandidate?> FindAsync(AlbumArtQuery query, CancellationToken cancellationToken)
    {
        var term = Uri.EscapeDataString($"{query.Artist} {query.Album}");
        using var document = await AlbumArtHttp.GetJsonAsync(http, $"https://api.deezer.com/search/album?q={term}&limit=10", cancellationToken);
        if (document is null || !document.RootElement.TryGetProperty("data", out var data))
            return null;

        var hits = data.EnumerateArray()
            .Select(a => new AlbumHit(
                a.StringOf("title"),
                a.TryGetProperty("artist", out var artist) ? artist.StringOf("name") : "",
                a.StringOf("cover_xl")))
            .Where(hit => hit.Reference.Length > 0)
            .ToList();

        var best = AlbumMatcher.PickBest(hits, query);
        if (best is null)
            return null;

        // cover_xl is 1000x1000; the same CDN path serves larger sizes when the source has them.
        if (best.Reference.Contains("1000x1000", StringComparison.Ordinal))
        {
            var larger = await TryDownloadAsync(best.Reference.Replace("1000x1000", "1400x1400"), best.Album, cancellationToken);
            if (larger is not null && larger.Width >= 1400)
                return larger;
        }

        return await AlbumArtHttp.DownloadImageAsync(http, best.Reference, best.Album, cancellationToken);
    }

    private async Task<AlbumArtCandidate?> TryDownloadAsync(string url, string title, CancellationToken cancellationToken)
    {
        try
        {
            return await AlbumArtHttp.DownloadImageAsync(http, url, title, cancellationToken);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            return null; // the larger size is a bonus (a timeout counts too) - fall back to the standard one
        }
    }
}
