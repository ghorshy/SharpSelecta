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

    [ObservableProperty]
    private AlbumSortMode sortMode;

    [ObservableProperty]
    private bool sortDescending;

    public BulkObservableCollection<AlbumRowViewModel> Rows { get; } = [];

    public AlbumGridViewModel(LibraryViewModel library, string settingsFilePath)
    {
        _library = library;
        _settingsFilePath = settingsFilePath;
        tileSize = Math.Clamp(LibrarySettingsStore.LoadTileSize(settingsFilePath) ?? DefaultTileSize, MinTileSize, MaxTileSize);
        sortMode = LibrarySettingsStore.LoadAlbumSortMode(settingsFilePath) ?? AlbumSortMode.Title;
        sortDescending = LibrarySettingsStore.LoadAlbumSortDescending(settingsFilePath) ?? false;

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

    partial void OnSortModeChanged(AlbumSortMode value)
    {
        LibrarySettingsStore.SaveAlbumSortMode(_settingsFilePath, value);
        RebuildRows(force: true);
    }

    partial void OnSortDescendingChanged(bool value)
    {
        LibrarySettingsStore.SaveAlbumSortDescending(_settingsFilePath, value);
        OnPropertyChanged(nameof(SortDirectionSymbol));
        RebuildRows(force: true);
    }

    // Plain up/down arrows rather than an icon font/asset - matches the rest of the grid's chrome
    // (the Slider above it, the "✕" close glyph on an expanded tile), which is all text/glyphs
    // rather than a bundled icon set.
    public string SortDirectionSymbol => SortDescending ? "↓" : "↑";

    [RelayCommand]
    private void SetSortMode(AlbumSortMode mode) => SortMode = mode;

    [RelayCommand]
    private void ToggleSortDirection() => SortDescending = !SortDescending;

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
    // or when the album list or sort changed (force=true, e.g. after a rescan or a sort-order
    // change) since the tiles themselves are different then regardless of column count.
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

        var rows = SortAlbums(_library.Albums)
            .Chunk(_columnCount)
            .Select(tiles => new AlbumRowViewModel(tiles));

        Rows.ReplaceAll(rows);
    }

    // _library.Albums itself always comes in Title-then-AlbumArtist order (see
    // LibraryViewModel.RebuildAlbums) - LINQ's OrderBy/OrderByDescending are stable, so whichever
    // mode below is picked, ties (e.g. two albums from the same Year) fall back to that original
    // alphabetical order for free instead of needing an explicit ThenBy here.
    private IEnumerable<AlbumViewModel> SortAlbums(IEnumerable<AlbumViewModel> albums) => SortMode switch
    {
        AlbumSortMode.Artist => SortDescending
            ? albums.OrderByDescending(a => a.Artist, StringComparer.OrdinalIgnoreCase)
            : albums.OrderBy(a => a.Artist, StringComparer.OrdinalIgnoreCase),

        // Untagged (null-Year) albums are always sorted to the end regardless of direction, rather
        // than jumping to the front when ascending just because null compares as the lowest value.
        AlbumSortMode.Year => SortDescending
            ? albums.OrderBy(a => a.Year is null).ThenByDescending(a => a.Year)
            : albums.OrderBy(a => a.Year is null).ThenBy(a => a.Year),

        _ => SortDescending
            ? albums.OrderByDescending(a => a.Title, StringComparer.OrdinalIgnoreCase)
            : albums.OrderBy(a => a.Title, StringComparer.OrdinalIgnoreCase),
    };

    private static int ComputeColumnCount(double viewportWidth, double tileSize)
    {
        if (viewportWidth <= 0)
            return 1;

        return Math.Max(1, (int)Math.Floor((viewportWidth + RowSpacing) / (tileSize + RowSpacing)));
    }
}
