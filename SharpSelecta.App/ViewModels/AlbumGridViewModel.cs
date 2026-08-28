using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Collections;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class AlbumGridViewModel : ViewModelBase
{
    private const double DefaultTileSize = 160;
    private const double MinTileSize = 144;
    private const double MaxTileSize = 320;
    private const double RowSpacing = 16;

    public const double TileSizeStep = 10;

    private readonly LibraryViewModel _library;
    private readonly string _settingsFilePath;
    private double _viewportWidth;
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
        tileSize = Math.Clamp(SettingsStore.LoadTileSize(settingsFilePath) ?? DefaultTileSize, MinTileSize, MaxTileSize);
        sortMode = SettingsStore.LoadAlbumSortMode(settingsFilePath) ?? AlbumSortMode.Title;
        sortDescending = SettingsStore.LoadAlbumSortDescending(settingsFilePath) ?? false;

        _library.Albums.CollectionChanged += (_, _) => RebuildRows(force: true);
        _library.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryViewModel.SearchQuery))
                RebuildRows(force: true);
        };
    }

    public void SetViewportWidth(double width)
    {
        if (width == _viewportWidth)
            return;

        _viewportWidth = width;
        RebuildRows(force: false);
    }

    public void AdjustTileSize(double delta) => TileSize = Math.Clamp(TileSize + delta, MinTileSize, MaxTileSize);

    [RelayCommand]
    private void IncreaseTileSize() => AdjustTileSize(TileSizeStep);

    [RelayCommand]
    private void DecreaseTileSize() => AdjustTileSize(-TileSizeStep);

    partial void OnTileSizeChanged(double value)
    {
        SettingsStore.SaveTileSize(_settingsFilePath, value);
        RebuildRows(force: false);
    }

    partial void OnSortModeChanged(AlbumSortMode value)
    {
        SettingsStore.SaveAlbumSortMode(_settingsFilePath, value);
        RebuildRows(force: true);
    }

    partial void OnSortDescendingChanged(bool value)
    {
        SettingsStore.SaveAlbumSortDescending(_settingsFilePath, value);
        OnPropertyChanged(nameof(SortDirectionSymbol));
        RebuildRows(force: true);
    }

    public string SortDirectionSymbol => SortDescending ? "↓" : "↑";

    [RelayCommand]
    private void SetSortMode(AlbumSortMode mode)
    {
        if (SortMode == mode)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortMode = mode;
            SortDescending = false;
        }
    }

    [RelayCommand]
    private void ToggleSortDirection() => SortDescending = !SortDescending;

    [RelayCommand]
    private void ToggleExpand(AlbumViewModel album) => SetExpandedAlbum(ExpandedAlbum == album ? null : album);

    public void ExpandAlbum(AlbumViewModel album) => SetExpandedAlbum(album);

    private void SetExpandedAlbum(AlbumViewModel? album)
    {
        ExpandedAlbum = album;

        foreach (var row in Rows)
        {
            row.ExpandedAlbum = album is not null && row.Tiles.Contains(album) ? album : null;
        }
    }

    private void RebuildRows(bool force)
    {
        var newColumnCount = ComputeColumnCount(_viewportWidth, TileSize);
        if (!force && newColumnCount == _columnCount)
            return;

        _columnCount = newColumnCount;
        ExpandedAlbum = null;

        var query = _library.SearchQuery;
        var albums = string.IsNullOrWhiteSpace(query)
            ? SortAlbums(_library.Albums)
            : _library.Albums
                .Select(a => (Album: a, Score: SearchScore(a, query)))
                .Where(x => x.Score is not null)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Album);

        var rows = albums
            .Chunk(_columnCount)
            .Select(tiles => new AlbumRowViewModel(tiles));

        Rows.ReplaceAll(rows);
    }

    private static int? SearchScore(AlbumViewModel album, string query)
    {
        int? best = null;

        var titleScore = FuzzySearch.Score(album.Title, query);
        if (titleScore is not null)
            best = titleScore.Value + 30;

        var artistScore = FuzzySearch.Score(album.Artist, query);
        if (artistScore is not null)
        {
            var weighted = artistScore.Value + 15;
            if (best is null || weighted > best)
                best = weighted;
        }

        foreach (var track in album.Tracks)
        {
            var trackScore = FuzzySearch.Score(track.Track, query);
            if (trackScore is not null && (best is null || trackScore.Value > best))
                best = trackScore.Value;
        }

        return best;
    }

    private IEnumerable<AlbumViewModel> SortAlbums(IEnumerable<AlbumViewModel> albums) => SortMode switch
    {
        AlbumSortMode.Artist => SortDescending
            ? albums.OrderByDescending(a => a.Artist, StringComparer.OrdinalIgnoreCase)
            : albums.OrderBy(a => a.Artist, StringComparer.OrdinalIgnoreCase),

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
