using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

public partial class AlbumGridView : UserControl
{
    private AlbumCoverWindow? _albumCoverWindow;

    public AlbumGridView()
    {
        InitializeComponent();

        Scroller.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    private void OnScrollerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is AlbumGridViewModel vm)
        {
            vm.SetViewportWidth(e.NewSize.Width);
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not AlbumGridViewModel vm || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        vm.AdjustTileSize(e.Delta.Y * 10);
        e.Handled = true;
    }

    private void OnTileTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: AlbumViewModel album } tile)
        {
            album.Library.Grid.ExpandAlbum(album);
            ScrollExpandedPanelIntoView(tile);
        }
    }

    private void OnTileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: AlbumViewModel album } tile)
        {
            album.Library.Grid.ExpandAlbum(album);
            album.Library.PlayAlbumNowCommand.Execute(album);
            ScrollExpandedPanelIntoView(tile);
        }
    }

    private static void ScrollExpandedPanelIntoView(Control tile)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var row = tile.FindAncestorOfType<StackPanel>()?.FindAncestorOfType<StackPanel>();
            var expandedPanel = row?.Children.OfType<Border>().FirstOrDefault();
            expandedPanel?.BringIntoView();
        }, DispatcherPriority.Loaded);
    }

    private void OnExpandedTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: AlbumTrackRowViewModel item })
        {
            item.Track.Library.PlayNowCommand.Execute(item.Track.Track);
        }
    }

    private async void OnExpandedArtworkDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: AlbumViewModel { ArtworkBytes: not null } album })
            return;

        if (_albumCoverWindow is not null)
        {
            _albumCoverWindow.Activate();
            return;
        }

        var firstTrackPath = album.Tracks.Count > 0 ? album.Tracks[0].Track.FilePath : null;
        var fullResolutionBytes = firstTrackPath is null
            ? null
            : await Task.Run(() => MusicLibraryScanner.LoadArtwork(firstTrackPath));

        _albumCoverWindow = new AlbumCoverWindow { DataContext = new FullResolutionArtwork(fullResolutionBytes ?? album.ArtworkBytes) };
        _albumCoverWindow.Closed += (_, _) => _albumCoverWindow = null;

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            _albumCoverWindow.Show(owner);
        }
        else
        {
            _albumCoverWindow.Show();
        }
    }

    private sealed class FullResolutionArtwork(byte[]? artworkBytes) : IArtworkPreview
    {
        public byte[]? ArtworkBytes { get; } = artworkBytes;
    }
}
