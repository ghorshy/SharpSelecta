using SharpSelecta.Core.Playback;

namespace SharpSelecta.App.ViewModels;

// Row-level wrapper so the Queue's context menu can reach the owning QueueViewModel's commands
// directly (strongly typed, never null) instead of climbing the visual tree via $parent/
// RelativeSource — which only exposes DataContext as an untyped, nullable object, and doesn't
// reliably resolve at all from within a ContextMenu popup (see LibraryTrackViewModel for the
// same reasoning applied to the Library view).
public sealed class QueueEntryViewModel(QueueEntry entry, QueueViewModel queue)
{
    public QueueEntry Entry { get; } = entry;

    public string DisplayName => Entry.Track.DisplayName;

    public QueueViewModel Queue { get; } = queue;
}
