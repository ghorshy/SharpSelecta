using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Collections;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class AlbumGridViewModel : ViewModelBase
{
    private const double DefaultTileSize = 160;
    private const double MinTileSize = 144;
    private const double MaxTileSize = 320;
    private const double RowSpacing = 16;

    public const double TileSizeStep = 10;

    private static readonly int ArtworkLoadConcurrency = Math.Max(1, Environment.ProcessorCount / 2);

    private readonly LibraryViewModel _library;
    private readonly string _settingsFilePath;
    private readonly ILogger _logger;
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

    public BulkObservableCollection<AlbumViewModel> Albums { get; } = [];

    public BulkObservableCollection<AlbumRowViewModel> Rows { get; } = [];

    public AlbumGridViewModel(LibraryViewModel library, string settingsFilePath, ILogger logger)
    {
        _library = library;
        _settingsFilePath = settingsFilePath;
        _logger = logger;
        tileSize = Math.Clamp(SettingsStore.LoadTileSize(settingsFilePath) ?? DefaultTileSize, MinTileSize, MaxTileSize);
        sortMode = SettingsStore.LoadAlbumSortMode(settingsFilePath) ?? AlbumSortMode.Title;
        sortDescending = SettingsStore.LoadAlbumSortDescending(settingsFilePath) ?? false;

        _library.Tracks.CollectionChanged += (_, _) => RebuildAlbums();
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

    private void RebuildAlbums()
    {
        var groups = _library.Tracks
            .GroupBy(
                t => (Album: (t.Track.Album ?? string.Empty).Trim(), AlbumArtist: (t.Track.AlbumArtist ?? string.Empty).Trim()),
                AlbumGroupKeyComparer.Instance)
            .OrderBy(g => g.Key.Album, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key.AlbumArtist, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var orderedTracks = g
                    .OrderBy(t => t.Track.TrackNumber ?? int.MaxValue)
                    .ThenBy(t => t.Track.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var album = new AlbumViewModel(
                    g.Key.Album.Length > 0 ? g.Key.Album : Strings.UnknownAlbum,
                    ResolveArtistLabel(orderedTracks),
                    orderedTracks.Select(t => t.Track.Year).FirstOrDefault(y => y.HasValue),
                    orderedTracks,
                    _library);
                return (RawKey: $"{g.Key.Album}{g.Key.AlbumArtist}", Album: album);
            })
            .ToList();

        Albums.ReplaceAll(groups.Select(g => g.Album));
        RebuildRows(force: true);

        _ = LoadAlbumArtworkAsync(groups);
    }

    private string ArtworkCacheDirectory => Path.Combine(Path.GetDirectoryName(_settingsFilePath)!, "artwork-cache");

    [RelayCommand]
    private void ClearArtworkCache()
    {
        if (Directory.Exists(ArtworkCacheDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(ArtworkCacheDirectory))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
        }

        RebuildAlbums();
    }

    private async Task LoadAlbumArtworkAsync(IReadOnlyList<(string RawKey, AlbumViewModel Album)> groups)
    {
        var cacheDirectory = ArtworkCacheDirectory;
        var options = new ParallelOptions { MaxDegreeOfParallelism = ArtworkLoadConcurrency };
        var stopwatch = Stopwatch.StartNew();

        await Parallel.ForEachAsync(groups, options, async (group, cancellationToken) =>
        {
            var (rawKey, album) = group;
            var firstTrackPath = album.Tracks.Count > 0 ? album.Tracks[0].Track.FilePath : null;
            if (firstTrackPath is null)
                return;

            try
            {
                var artwork = AlbumArtworkCache.GetOrCreate(
                    cacheDirectory, rawKey, () => MusicLibraryScanner.LoadArtwork(firstTrackPath));

                await Dispatcher.UIThread.InvokeAsync(() => album.ArtworkBytes = artwork);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load artwork for album {Album}", album.Title);
            }
        });

        _logger.LogInformation("Loaded artwork for {AlbumCount} albums in {ElapsedMs} ms", groups.Count, stopwatch.ElapsedMilliseconds);
    }

    private sealed class AlbumGroupKeyComparer : IEqualityComparer<(string Album, string AlbumArtist)>
    {
        public static readonly AlbumGroupKeyComparer Instance = new();

        public bool Equals((string Album, string AlbumArtist) x, (string Album, string AlbumArtist) y) =>
            string.Equals(x.Album, y.Album, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.AlbumArtist, y.AlbumArtist, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Album, string AlbumArtist) key) =>
            HashCode.Combine(
                key.Album.ToUpperInvariant(),
                key.AlbumArtist.ToUpperInvariant());
    }

    private static string ResolveArtistLabel(IEnumerable<LibraryTrackViewModel> tracks)
    {
        var distinctArtists = tracks
            .Select(t => (t.Track.Artist ?? string.Empty).Trim())
            .Where(artist => artist.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return distinctArtists.Count switch
        {
            0 => string.Empty,
            1 => distinctArtists[0],
            _ => Strings.VariousArtists,
        };
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
            ? SortAlbums(Albums)
            : Albums
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
