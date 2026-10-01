using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.AlbumArt;

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
            .Select(a => new Hit(
                a.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                a.TryGetProperty("artist", out var artist) && artist.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                a.TryGetProperty("cover_xl", out var cover) ? cover.GetString() ?? "" : ""))
            .Where(hit => hit.CoverUrl.Length > 0)
            .ToList();

        var best = AlbumMatcher.PickBest(hits, query, hit => hit.Album, hit => hit.Artist);
        if (best is null)
            return null;

        // cover_xl is 1000x1000; the same CDN path serves larger sizes when the source has them.
        if (best.CoverUrl.Contains("1000x1000", StringComparison.Ordinal))
        {
            var larger = await TryDownloadAsync(best.CoverUrl.Replace("1000x1000", "1400x1400"), best.Album, cancellationToken);
            if (larger is not null && larger.Width >= 1400)
                return larger;
        }

        return await AlbumArtHttp.DownloadImageAsync(http, best.CoverUrl, best.Album, cancellationToken);
    }

    private sealed record Hit(string Album, string Artist, string CoverUrl);

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
