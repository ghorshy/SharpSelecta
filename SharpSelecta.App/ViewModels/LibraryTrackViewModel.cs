using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// Row-level wrapper so the Library's DataTemplate can reach the owning LibraryViewModel's
// commands directly (strongly typed, never null) instead of climbing the visual tree via
// $parent — which only exposes DataContext as an untyped, nullable object.
public sealed class LibraryTrackViewModel(Track track, LibraryViewModel library)
{
    public Track Track { get; } = track;

    public string DisplayName => Track.DisplayName;

    public LibraryViewModel Library { get; } = library;
}
