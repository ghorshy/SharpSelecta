using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.Integrations.AlbumArt;

// Cover Art Archive (MusicBrainz's cover project): find the album on MusicBrainz, then ask for its front cover.
public sealed class CoverArtArchiveProvider(HttpClient http, MusicBrainzClient musicBrainz) : IAlbumArtProvider
{
    public string Name => "Cover Art Archive";

    public async Task<AlbumArtCandidate?> FindAsync(AlbumArtQuery query, CancellationToken cancellationToken)
    {
        var releaseGroup = await musicBrainz.FindReleaseGroupAsync(query, cancellationToken);
        return releaseGroup is null
            ? null
            : await AlbumArtHttp.DownloadImageAsync(
                http, $"https://coverartarchive.org/release-group/{releaseGroup.Id}/front-1200", releaseGroup.Title, cancellationToken);
    }
}
