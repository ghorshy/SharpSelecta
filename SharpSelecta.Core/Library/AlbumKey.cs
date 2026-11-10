namespace SharpSelecta.Core.Library;

public readonly record struct AlbumKey(string Album, string AlbumArtist)
{
    public static AlbumKey Of(Track track) =>
        new((track.Album ?? string.Empty).Trim(), (track.AlbumArtist ?? string.Empty).Trim());

    public bool Equals(AlbumKey other) =>
        string.Equals(Album, other.Album, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(AlbumArtist, other.AlbumArtist, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        HashCode.Combine(Album.ToUpperInvariant(), AlbumArtist.ToUpperInvariant());
}
