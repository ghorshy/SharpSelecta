using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.AlbumArt;

public sealed record MusicBrainzReleaseGroup(string Id, string Title);

// Looks up an album's release-group id, which Cover Art Archive and fanart.tv are both keyed by.
// MusicBrainz allows one request per second, so lookups are spaced out, and the same query asked
// by both providers is answered once.
public sealed class MusicBrainzClient(HttpClient http, TimeSpan? minimumRequestInterval = null)
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(1100);

    private readonly TimeSpan _interval = minimumRequestInterval ?? DefaultInterval;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _lock = new();
    private DateTime _lastRequestUtc = DateTime.MinValue;
    private (AlbumArtQuery Query, Task<MusicBrainzReleaseGroup?> Lookup)? _latest;

    public Task<MusicBrainzReleaseGroup?> FindReleaseGroupAsync(AlbumArtQuery query, CancellationToken cancellationToken)
    {
        Task<MusicBrainzReleaseGroup?> lookup;
        lock (_lock)
        {
            if (_latest is { } latest && latest.Query == query && !latest.Lookup.IsFaulted)
            {
                lookup = latest.Lookup;
            }
            else
            {
                // Shared between providers, so it isn't tied to either one's cancellation.
                lookup = LookupAsync(query);
                _latest = (query, lookup);
            }
        }

        return lookup.WaitAsync(cancellationToken);
    }

    private async Task<MusicBrainzReleaseGroup?> LookupAsync(AlbumArtQuery query)
    {
        await _gate.WaitAsync();
        try
        {
            var wait = _interval - (DateTime.UtcNow - _lastRequestUtc);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait);
            }

            _lastRequestUtc = DateTime.UtcNow;

            var search = $"releasegroup:\"{Escape(query.Album)}\" AND artist:\"{Escape(query.Artist)}\"";
            var url = $"https://musicbrainz.org/ws/2/release-group/?query={Uri.EscapeDataString(search)}&fmt=json&limit=10";
            using var document = await AlbumArtHttp.GetJsonAsync(http, url, CancellationToken.None);
            if (document is null || !document.RootElement.TryGetProperty("release-groups", out var groups))
                return null;

            var hits = groups.EnumerateArray()
                .Select(group => new Hit(
                    group.GetProperty("id").GetString() ?? "",
                    group.TryGetProperty("title", out var title) ? title.GetString() : null,
                    ArtistOf(group)))
                .ToList();

            var best = AlbumMatcher.PickBest(hits, query, hit => hit.Title, hit => hit.Artist);
            return best is null ? null : new MusicBrainzReleaseGroup(best.Id, best.Title ?? query.Album);
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record Hit(string Id, string? Title, string? Artist);

    private static string? ArtistOf(System.Text.Json.JsonElement group) =>
        group.TryGetProperty("artist-credit", out var credit) && credit.GetArrayLength() > 0
            ? string.Concat(credit.EnumerateArray().Select(c => (c.TryGetProperty("name", out var n) ? n.GetString() : "") + (c.TryGetProperty("joinphrase", out var j) ? j.GetString() : "")))
            : null;

    // Lucene special characters inside a quoted phrase.
    private static string Escape(string value) => value.Replace("\\", " ").Replace("\"", " ");
}
