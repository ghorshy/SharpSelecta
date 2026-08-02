using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.App.ViewModels;

public partial class QueueEntryViewModel : ViewModelBase
{
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

    public bool IsCurrent => Queue.Entries.IndexOf(this) == Queue.CurrentIndex;

    public void NotifyIsCurrentChanged() => OnPropertyChanged(nameof(IsCurrent));

    private async Task LoadArtworkAsync() =>
        ArtworkBytes = await Task.Run(() => MusicLibraryScanner.LoadArtwork(Entry.Track.FilePath));
}
