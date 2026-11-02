using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.Integrations.AlbumArt;

public sealed record MusicBrainzReleaseGroup(string Id, string Title);

// MusicBrainz allows one request per second; the same query from both providers is answered once.
public sealed class MusicBrainzClient(HttpClient http, TimeSpan? minimumRequestInterval = null, TimeProvider? time = null)
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(1100);

    private readonly TimeSpan _interval = minimumRequestInterval ?? DefaultInterval;
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _lock = new();
    private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;
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
            var wait = _interval - (_time.GetUtcNow() - _lastRequestUtc);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, _time);
            }

            _lastRequestUtc = _time.GetUtcNow();

            var search = $"releasegroup:\"{Escape(query.Album)}\" AND artist:\"{Escape(query.Artist)}\"";
            var url = $"https://musicbrainz.org/ws/2/release-group/?query={Uri.EscapeDataString(search)}&fmt=json&limit=10";
            using var document = await AlbumArtHttp.GetJsonAsync(http, url, CancellationToken.None);
            if (document is null || !document.RootElement.TryGetProperty("release-groups", out var groups))
                return null;

            var hits = groups.EnumerateArray()
                .Select(group => new AlbumHit(group.StringOf("title"), ArtistOf(group), group.StringOf("id")))
                .Where(hit => hit.Reference.Length > 0)
                .ToList();

            var best = AlbumMatcher.PickBest(hits, query);
            return best is null ? null : new MusicBrainzReleaseGroup(best.Reference, best.Album.Length > 0 ? best.Album : query.Album);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string ArtistOf(System.Text.Json.JsonElement group) =>
        group.TryGetProperty("artist-credit", out var credit)
            ? string.Concat(credit.EnumerateArray().Select(c => c.StringOf("name") + c.StringOf("joinphrase")))
            : "";

    // Lucene special characters inside a quoted phrase.
    private static string Escape(string value) => value.Replace("\\", " ").Replace("\"", " ");
}
