using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.App.ViewModels;

// Row-level wrapper so the Queue's context menu can reach the owning QueueViewModel's commands
// directly (strongly typed, never null) instead of climbing the visual tree via $parent/
// RelativeSource — which only exposes DataContext as an untyped, nullable object, and doesn't
// reliably resolve at all from within a ContextMenu popup (see LibraryTrackViewModel for the
// same reasoning applied to the Library view).
public partial class QueueEntryViewModel : ViewModelBase
{
    // Raw bytes rather than an Avalonia Bitmap — see PlaybackControlsViewModel.CurrentTrackArtworkBytes
    // for why. Loaded lazily per row instead of during MusicLibraryScanner.Scan, same reasoning as
    // MusicLibraryScanner.LoadArtwork itself: decoding embedded pictures upfront for every track would
    // be wasteful when most never end up queued.
    [ObservableProperty]
    private byte[]? artworkBytes;

    public QueueEntryViewModel(QueueEntry entry, QueueViewModel queue)
    {
        Entry = entry;
        Queue = queue;
        _ = LoadArtworkAsync();
    }

    public QueueEntry Entry { get; }

    public QueueViewModel Queue { get; }

    public string DisplayName => Entry.Track.DisplayName;

    public string Title => Entry.Track.DisplayName;

    public string Artist => Entry.Track.Artist ?? string.Empty;

    // The queue's current entry, not the ListBox's selected row — those are different things
    // (clicking a row to select it, e.g. before dragging it, shouldn't relabel it as playing).
    public bool IsCurrent => Queue.Entries.IndexOf(this) == Queue.CurrentIndex;

    public void NotifyIsCurrentChanged() => OnPropertyChanged(nameof(IsCurrent));

    private async Task LoadArtworkAsync() =>
        ArtworkBytes = await Task.Run(() => MusicLibraryScanner.LoadArtwork(Entry.Track.FilePath));
}
