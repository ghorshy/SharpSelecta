using System.Globalization;

using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.Integrations.AlbumArt;

// fanart.tv's album covers (1000x1000, user-curated). Needs an API key; keyed by the MusicBrainz release-group id.
public sealed class FanartTvArtProvider(
    HttpClient http, MusicBrainzClient musicBrainz, Func<string?> loadApiKey, Action<string> saveApiKey) : IAlbumArtProvider
{
    public string Name => "fanart.tv";

    public bool CanBeConfigured => true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(loadApiKey());

    public void Configure(string value) => saveApiKey(value.Trim());

    public async Task<AlbumArtCandidate?> FindAsync(AlbumArtQuery query, CancellationToken cancellationToken)
    {
        if (loadApiKey()?.Trim() is not { Length: > 0 } apiKey)
            return null;

        var releaseGroup = await musicBrainz.FindReleaseGroupAsync(query, cancellationToken);
        if (releaseGroup is null)
            return null;

        var url = $"https://webservice.fanart.tv/v3/music/albums/{releaseGroup.Id}?api_key={Uri.EscapeDataString(apiKey)}";
        using var document = await AlbumArtHttp.GetJsonAsync(http, url, cancellationToken);
        if (document is null || !document.RootElement.TryGetProperty("albums", out var albums))
            return null;

        // The endpoint answers with just the requested album, keyed by its release-group id.
        if (!albums.TryGetProperty(releaseGroup.Id, out var album) || !album.TryGetProperty("albumcover", out var covers))
            return null;

        var best = covers.EnumerateArray()
            .Select(c => (Url: c.TryGetProperty("url", out var u) ? u.GetString() : null, Likes: Likes(c)))
            .Where(c => !string.IsNullOrEmpty(c.Url))
            .OrderByDescending(c => c.Likes)
            .FirstOrDefault();
        if (best.Url is null)
            return null;

        var imageUrl = best.Url.Replace("http://", "https://", StringComparison.Ordinal);
        return await AlbumArtHttp.DownloadImageAsync(http, imageUrl, releaseGroup.Title, cancellationToken);
    }

    private static int Likes(System.Text.Json.JsonElement cover) =>
        cover.TryGetProperty("likes", out var likes) && int.TryParse(likes.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
