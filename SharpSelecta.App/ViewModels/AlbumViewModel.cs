using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// Row-level wrapper for the album grid view, analogous to LibraryTrackViewModel for the track
// DataGrid. Tracks are the same LibraryTrackViewModel instances LibraryViewModel.Tracks already
// holds (grouped by album), not freshly-wrapped copies, so identity-based checks elsewhere
// (e.g. "is this the currently playing row") keep working.
//
// ArtworkBytes starts null and is filled in later by LibraryViewModel once the disk-backed
// artwork cache has decoded/resized it on a background thread - hence ObservableObject rather
// than the plain class LibraryTrackViewModel gets away with, so the tile updates once it arrives.
public sealed partial class AlbumViewModel(string title, string artist, IReadOnlyList<LibraryTrackViewModel> tracks, LibraryViewModel library) : ObservableObject
{
    public string Title { get; } = title;

    public string Artist { get; } = artist;

    public IReadOnlyList<LibraryTrackViewModel> Tracks { get; } = tracks;

    public IReadOnlyList<Track> UnderlyingTracks { get; } = tracks.Select(t => t.Track).ToList();

    public LibraryViewModel Library { get; } = library;

    [ObservableProperty]
    private byte[]? artworkBytes;
}
