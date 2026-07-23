using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Collections;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// Owns the album grid's layout state (tile size/zoom, computed rows, which album is expanded) —
// split out from LibraryViewModel because none of this is track/album data, it's grid-layout
// mechanics that WrapPanel can't do on its own (see AlbumRowViewModel).
public partial class AlbumGridViewModel : ViewModelBase
{
    private const double DefaultTileSize = 160;
    private const double MinTileSize = 80;
    private const double MaxTileSize = 320;
    private const double RowSpacing = 8;

    private readonly LibraryViewModel _library;
    private readonly string _settingsFilePath;
    private double _viewportWidth;
    private int _columnCount = 1;

    [ObservableProperty]
    private double tileSize;

    [ObservableProperty]
    private AlbumViewModel? expandedAlbum;

    public BulkObservableCollection<AlbumRowViewModel> Rows { get; } = [];

    public AlbumGridViewModel(LibraryViewModel library, string settingsFilePath)
    {
        _library = library;
        _settingsFilePath = settingsFilePath;
        tileSize = Math.Clamp(LibrarySettingsStore.LoadTileSize(settingsFilePath) ?? DefaultTileSize, MinTileSize, MaxTileSize);

        _library.Albums.CollectionChanged += (_, _) => RecomputeLayout();
    }

    // Fed by the View's SizeChanged handler — WrapPanel can't tell us this itself since we're not
    // using one; we compute the column count ourselves instead of leaving layout to a panel.
    public void SetViewportWidth(double width)
    {
        if (width == _viewportWidth)
            return;

        _viewportWidth = width;
        RecomputeLayout();
    }

    public void AdjustTileSize(double delta) => TileSize = Math.Clamp(TileSize + delta, MinTileSize, MaxTileSize);

    partial void OnTileSizeChanged(double value)
    {
        LibrarySettingsStore.SaveTileSize(_settingsFilePath, value);
        RecomputeLayout();
    }

    [RelayCommand]
    private void ToggleExpand(AlbumViewModel album) => SetExpandedAlbum(ExpandedAlbum == album ? null : album);

    private void SetExpandedAlbum(AlbumViewModel? album)
    {
        ExpandedAlbum = album;

        foreach (var row in Rows)
        {
            row.ExpandedAlbum = album is not null && row.Tiles.Contains(album) ? album : null;
        }
    }

    // Any layout-affecting change (viewport resize, zoom, or the album list itself changing after
    // a rescan) collapses whatever was expanded rather than trying to re-attach it to whichever
    // row it lands in after repartitioning — simpler, and resizing/zooming while reading an
    // expanded album's tracklist is an edge case not worth the extra complexity.
    private void RecomputeLayout()
    {
        _columnCount = ComputeColumnCount(_viewportWidth, TileSize);
        ExpandedAlbum = null;

        var rows = _library.Albums
            .Chunk(_columnCount)
            .Select(tiles => new AlbumRowViewModel(tiles));

        Rows.ReplaceAll(rows);
    }

    private static int ComputeColumnCount(double viewportWidth, double tileSize)
    {
        if (viewportWidth <= 0 || tileSize <= 0)
            return 1;

        return Math.Max(1, (int)Math.Floor((viewportWidth + RowSpacing) / (tileSize + RowSpacing)));
    }
}
