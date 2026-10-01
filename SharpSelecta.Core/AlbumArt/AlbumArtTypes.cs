namespace SharpSelecta.Core.AlbumArt;

public sealed record AlbumArtQuery(string Artist, string Album);

// A cover a provider found, already downloaded; MatchedTitle is the album name the service matched.
public sealed record AlbumArtCandidate(byte[] Image, int Width, int Height, string MatchedTitle);

public interface IAlbumArtProvider
{
    string Name { get; }

    // False while the provider still needs configuring (an API key); FindAsync then finds nothing.
    bool IsConfigured => true;

    // True for a provider whose setting (an API key) the user can enter or replace - offered again even
    // after a failed search, since a rejected key is the likely reason.
    bool CanBeConfigured => false;

    void Configure(string value)
    {
    }

    // Null when the service has nothing for this album; throws when the service can't be reached or
    // answers with an error.
    Task<AlbumArtCandidate?> FindAsync(AlbumArtQuery query, CancellationToken cancellationToken);
}
