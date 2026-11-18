using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SharpSelecta.App.ViewModels;

public sealed partial class AlbumTrackRowViewModel(LibraryTrackViewModel track, string? artistSuffix, int position) : ObservableObject
{
    public LibraryTrackViewModel Track { get; } = track;

    // The tagged track number, or the row's place in the album when the file has none.
    public string NumberDisplay { get; } = (track.Track.TrackNumber ?? position).ToString(CultureInfo.CurrentCulture);

    public string Title { get; } = track.Track.Title ?? track.DisplayName;

    public string LengthDisplay { get; } = track.LengthDisplay;

    // e.g. "(K Dot & DJ Q)" - null when the whole album shares one artist.
    public string? ArtistSuffix { get; } = artistSuffix;

    [ObservableProperty] public partial bool IsPlaying { get; set; }
}
