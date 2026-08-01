namespace SharpSelecta.App.ViewModels;

public sealed class AlbumTrackRowViewModel(LibraryTrackViewModel track, string? artistSuffix)
{
    public LibraryTrackViewModel Track { get; } = track;

    public int? TrackNumber { get; } = track.Track.TrackNumber;

    public string Title { get; } = track.Track.Title ?? track.DisplayName;

    public string LengthDisplay { get; } = track.LengthDisplay;

    // e.g. "(K Dot & DJ Q)" - null when the whole album shares one artist.
    public string? ArtistSuffix { get; } = artistSuffix;
}
