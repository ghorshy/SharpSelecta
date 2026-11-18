using System.ComponentModel;
using Avalonia;
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
        Scroller.AddHandler(InputElement.PointerPressedEvent, OnTilePointerPressed, handledEventsToo: true);
        Scroller.AddHandler(InputElement.PointerReleasedEvent, (_, _) => ReleasePressedTile(), handledEventsToo: true);
        Scroller.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => ReleasePressedTile(), handledEventsToo: true);
        var drag = new TrackDragSource(Scroller, DraggedTracks)
        {
            OnError = ex => (DataContext as AlbumGridViewModel)?.ReportDragFailure(ex),
        };
        Border? draggedTile = null;
        drag.Starting += (_, source) =>
        {
            ReleasePressedTile();
            draggedTile = AlbumTile(source);
            draggedTile?.Classes.Add("dragging");
        };
        drag.Finished += (_, _) =>
        {
            draggedTile?.Classes.Remove("dragging");
            draggedTile = null;
        };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is AlbumGridViewModel vm)
                vm.RevealRequested += (_, reveal) => Dispatcher.UIThread.Post(() => Reveal(vm, reveal), DispatcherPriority.Loaded);
        };
    }

    private Border? _pressedTile;

    private void OnTilePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Scroller).Properties.IsLeftButtonPressed || e.Source is not Visual source)
            return;

        ReleasePressedTile();
        _pressedTile = AlbumTile(source);
        _pressedTile?.Classes.Add("pressed");
    }

    private void ReleasePressedTile()
    {
        _pressedTile?.Classes.Remove("pressed");
        _pressedTile = null;
    }

    // A row of an expanded album drags that track; a tile its album, or every selected album when it is one of them.
    private static IReadOnlyList<Track>? DraggedTracks(Visual source)
    {
        if (source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { DataContext: AlbumTrackRowViewModel row })
            return [row.Track.Track];

        return AlbumTile(source) is { DataContext: AlbumViewModel album } ? album.Library.ResolveAlbumSelection(album) : null;
    }

    private static Border? AlbumTile(Visual source) =>
        source.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("album-tile"));

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

    // Ctrl (Cmd) adds or removes a tile and Shift selects a range, like in the track list; a plain
    // click selects and expands just that album.
    private void OnTileTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: AlbumViewModel album } tile || DataContext is not AlbumGridViewModel vm)
            return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            vm.SelectRangeTo(album);
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            vm.ToggleSelected(album);
        }
        else
        {
            vm.Select(album);
            vm.ExpandAlbum(album);
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
            CompilationFlagMenu.Refresh(menu, album.Library, album.Library.ResolveAlbumSelection(album));
        }
    }

    private async void OnAlbumCompilationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AlbumViewModel album } item)
        {
            await CompilationFlagMenu.ToggleAsync(item, album.Library, album.Library.ResolveAlbumSelection(album));
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
            await TrackPropertiesWindow.ShowAsync(this, album.Library, album.Library.ResolveAlbumSelection(album));
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
