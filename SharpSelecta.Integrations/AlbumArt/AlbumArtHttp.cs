using System.Net;

using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.Integrations.AlbumArt;

public static class AlbumArtHttp
{
    private const int MaxImageBytes = 20 * 1024 * 1024;

    // MusicBrainz (and good manners elsewhere) want an identifiable User-Agent with a way to reach us.
    public static HttpClient CreateClient()
    {
        var version = typeof(AlbumArtHttp).Assembly.GetName().Version?.ToString(3) ?? "0";
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"SharpSelecta/{version} (https://github.com/ghorshy/SharpSelecta)");
        return client;
    }

    // Downloads an image; null when the URL 404s or what came back isn't a JPEG/PNG.
    internal static async Task<AlbumArtCandidate?> DownloadImageAsync(
        HttpClient http, string url, string matchedTitle, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxImageBytes)
            return null;

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return ImageSize.TryRead(bytes) is { } size && bytes.Length <= MaxImageBytes
            ? new AlbumArtCandidate(bytes, size.Width, size.Height, matchedTitle)
            : null;
    }

    internal static async Task<System.Text.Json.JsonDocument?> GetJsonAsync(
        HttpClient http, string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
