namespace SharpSelecta.Integrations.AlbumArt;

// One search result: Reference is what the provider needs next (an artwork URL, a release-group id).
internal sealed record AlbumHit(string Album, string Artist, string Reference);
