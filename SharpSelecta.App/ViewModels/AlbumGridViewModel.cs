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

    // Raised from 80 - a smaller minimum let more tiles fit on screen at once, and since the grid
    // isn't virtualized *within* the viewport (only off-screen rows are skipped), more visible
    // tiles means more concurrent artwork decode/render work competing with the real-time audio
    // thread for CPU, causing buffer overruns again even after the ItemsRepeater virtualization fix.
    private const double MinTileSize = 144;
    private const double MaxTileSize = 320;

    // Must match the actual tile Spacing/Margin in AlbumGridView.axaml (Spacing.L) - this is what
    // the column-count math below assumes the real per-tile gap is.
    private const double RowSpacing = 16;

    private readonly LibraryViewModel _library;
    private readonly string _settingsFilePath;
    private double _viewportWidth;

    // -1 (not a valid column count) so the very first RebuildRows call always proceeds, even if
    // the first real computation happens to come out to the same value a default like 1 would have.
    private int _columnCount = -1;

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

        _library.Albums.CollectionChanged += (_, _) => RebuildRows(force: true);
    }

    // Fed by the View's SizeChanged handler — WrapPanel can't tell us this itself since we're not
    // using one; we compute the column count ourselves instead of leaving layout to a panel.
    public void SetViewportWidth(double width)
    {
        if (width == _viewportWidth)
            return;

        _viewportWidth = width;
        RebuildRows(force: false);
    }

    public void AdjustTileSize(double delta) => TileSize = Math.Clamp(TileSize + delta, MinTileSize, MaxTileSize);

    partial void OnTileSizeChanged(double value)
    {
        LibrarySettingsStore.SaveTileSize(_settingsFilePath, value);
        RebuildRows(force: false);
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

    // Rebuilding Rows means Avalonia has to tear down and reconstruct every tile's visual tree
    // (the grid isn't virtualized) — expensive for a large library, and was happening on every
    // single TileSize tick during a slider drag or Ctrl+scroll, most of which don't actually change
    // how many tiles fit per row. TileSize changes reflow already-realized tiles for free via their
    // own Width/Height bindings, so a rebuild is only needed when the column count itself changes,
    // or when the album list changed (force=true, e.g. after a rescan) since the tiles themselves
    // are different then regardless of column count.
    //
    // Any real rebuild collapses whatever was expanded rather than trying to re-attach it to
    // whichever row it lands in after repartitioning — simpler, and resizing/zooming while reading
    // an expanded album's tracklist is an edge case not worth the extra complexity.
    private void RebuildRows(bool force)
    {
        var newColumnCount = ComputeColumnCount(_viewportWidth, TileSize);
        if (!force && newColumnCount == _columnCount)
            return;

        _columnCount = newColumnCount;
        ExpandedAlbum = null;

        var rows = _library.Albums
            .Chunk(_columnCount)
            .Select(tiles => new AlbumRowViewModel(tiles));

        Rows.ReplaceAll(rows);
    }

    private static int ComputeColumnCount(double viewportWidth, double tileSize)
    {
        if (viewportWidth <= 0)
            return 1;

        return Math.Max(1, (int)Math.Floor((viewportWidth + RowSpacing) / (tileSize + RowSpacing)));
    }
}
