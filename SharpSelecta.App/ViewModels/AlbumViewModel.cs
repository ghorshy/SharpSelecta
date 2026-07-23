using System.Collections.Generic;

namespace SharpSelecta.App.ViewModels;

// Row-level wrapper for the album grid view, analogous to LibraryTrackViewModel for the track
// DataGrid. Tracks are the same LibraryTrackViewModel instances LibraryViewModel.Tracks already
// holds (grouped by album), not freshly-wrapped copies, so identity-based checks elsewhere
// (e.g. "is this the currently playing row") keep working.
public sealed class AlbumViewModel(string title, string artist, IReadOnlyList<LibraryTrackViewModel> tracks, LibraryViewModel library)
{
    public string Title { get; } = title;

    public string Artist { get; } = artist;

    public IReadOnlyList<LibraryTrackViewModel> Tracks { get; } = tracks;

    public LibraryViewModel Library { get; } = library;
}
