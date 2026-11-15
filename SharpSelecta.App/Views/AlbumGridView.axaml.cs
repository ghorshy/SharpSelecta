using System.ComponentModel;
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
        DataContextChanged += (_, _) =>
        {
            if (DataContext is AlbumGridViewModel vm)
                vm.RevealRequested += (_, reveal) => Dispatcher.UIThread.Post(() => Reveal(vm, reveal), DispatcherPriority.Loaded);
        };
    }

    // Rows are virtualized, so the album's row may not be realized yet.
    private void Reveal(AlbumGridViewModel vm, AlbumTrackReveal reveal)
    {
        var rowIndex = vm.Rows.ToList().FindIndex(r => r.Tiles.Contains(reveal.Album));
        if (rowIndex < 0)
            return;

        if (vm.ExpandedAlbum != reveal.Album)
            vm.ExpandAlbum(reveal.Album);

        var row = RowsRepeater.GetOrCreateElement(rowIndex);
        row.UpdateLayout();
        row.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Child is ContentControl)?.BringIntoView();

        if (row.GetVisualDescendants().OfType<ListBox>().FirstOrDefault() is not { } tracks)
            return;

        tracks.SelectedItem = reveal.Track;
        if (tracks.ContainerFromItem(reveal.Track) is { } container)
        {
            container.BringIntoView();
            container.Focus();
        }
    }

    private void OnScrollerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is AlbumGridViewModel vm)
        {
            vm.SetViewportWidth(e.NewSize.Width);
        }
    }

    private void OnTileTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: AlbumViewModel album } tile)
        {
            (DataContext as AlbumGridViewModel)?.ExpandAlbum(album);
            ScrollExpandedPanelIntoView(tile);
        }
    }

    private async void OnTileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: AlbumViewModel album } tile)
        {
            (DataContext as AlbumGridViewModel)?.ExpandAlbum(album);
            ScrollExpandedPanelIntoView(tile);
            await album.Library.StartTracksAsync(album.UnderlyingTracks);
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

    private async void OnExpandedTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: AlbumTrackRowViewModel item } row)
        {
            var album = row.FindAncestorOfType<ListBox>()?.DataContext as AlbumViewModel;
            IReadOnlyList<LibraryTrackViewModel> shown = album is null ? [item.Track] : [.. album.TrackRows.Select(r => r.Track)];
            await item.Track.Library.StartTrackAsync(item.Track, shown);
        }
    }

    private void OnAlbumAddToPlaylistSubmenuOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AlbumViewModel album } submenu)
        {
            AddToPlaylistMenu.Rebuild(submenu, album.Library.Playlists,
                playlistId => album.Library.AddAlbumToPlaylistCommand.Execute((album, playlistId)));
        }
    }

    private void OnTrackAddToPlaylistSubmenuOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AlbumTrackRowViewModel { Track: var trackItem } } submenu)
        {
            AddToPlaylistMenu.Rebuild(submenu, trackItem.Library.Playlists,
                playlistId => trackItem.Library.AddToPlaylistCommand.Execute((trackItem.Track, playlistId)));
        }
    }

    private void OnAlbumMenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is ContextMenu { DataContext: AlbumViewModel album } menu)
        {
            CompilationFlagMenu.Refresh(menu, album.Library, album.UnderlyingTracks);
        }
    }

    private async void OnAlbumCompilationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AlbumViewModel album } item)
        {
            await CompilationFlagMenu.ToggleAsync(item, album.Library, album.UnderlyingTracks);
        }
    }

    private void OnTrackMenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is ContextMenu { DataContext: AlbumTrackRowViewModel { Track: var trackItem } } menu)
        {
            CompilationFlagMenu.Refresh(menu, trackItem.Library, [trackItem.Track]);
        }
    }

    private async void OnTrackCompilationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AlbumTrackRowViewModel { Track: var trackItem } } item)
        {
            await CompilationFlagMenu.ToggleAsync(item, trackItem.Library, [trackItem.Track]);
        }
    }

    private async void OnAlbumPropertiesClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AlbumViewModel album })
        {
            await TrackPropertiesWindow.ShowAsync(this, album.Library, album.UnderlyingTracks);
        }
    }

    private async void OnTrackPropertiesClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AlbumTrackRowViewModel { Track: var trackItem } })
        {
            await TrackPropertiesWindow.ShowAsync(this, trackItem.Library, trackItem.Track);
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
