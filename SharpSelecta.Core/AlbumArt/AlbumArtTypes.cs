namespace SharpSelecta.Core.AlbumArt;

public sealed record AlbumArtQuery(string Artist, string Album);

// A cover a provider found, already downloaded; MatchedTitle is the album name the service matched.
public sealed record AlbumArtCandidate(byte[] Image, int Width, int Height, string MatchedTitle);

public interface IAlbumArtProvider
{
    string Name { get; }

    // Null when the service has nothing for this album; throws when the service can't be reached or
    // answers with an error.
    Task<AlbumArtCandidate?> FindAsync(AlbumArtQuery query, CancellationToken cancellationToken);
}

// A provider with a setting (an API key) the user can enter.
public interface IConfigurableAlbumArtProvider : IAlbumArtProvider
{
    // False while the setting is missing; FindAsync then finds nothing.
    bool IsConfigured { get; }

    void Configure(string value);
}
