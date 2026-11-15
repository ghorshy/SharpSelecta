using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Collections;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.App.Styles;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class AlbumGridViewModel : ViewModelBase
{
    private readonly ThemeLayout _layout;
    private readonly bool _limitToRecentlyAdded;

    private static readonly int ArtworkLoadConcurrency = Math.Max(1, Environment.ProcessorCount / 2);

    private readonly LibraryViewModel _library;
    private readonly SettingsStore _settings;
    private readonly ILogger _logger;
    private double _viewportWidth;
    private int _columnCount = -1;

    [ObservableProperty]
    public partial double TileSize { get; set; }

    [ObservableProperty]
    public partial AlbumViewModel? ExpandedAlbum { get; private set; }

    [ObservableProperty]
    public partial AlbumSortMode SortMode { get; private set; }

    [ObservableProperty]
    public partial bool SortDescending { get; private set; }
    public BulkObservableCollection<AlbumViewModel> Albums { get; } = [];

    public BulkObservableCollection<AlbumRowViewModel> Rows { get; } = [];

    public bool AllowUserSort { get; }

    public AlbumGridViewModel(LibraryViewModel library, SettingsStore settings, ThemeLayout layout, ILogger logger, bool allowUserSort = true, bool limitToRecentlyAdded = false)
    {
        _limitToRecentlyAdded = limitToRecentlyAdded;
        _library = library;
        _layout = layout;
        _settings = settings;
        _logger = logger;
        AllowUserSort = allowUserSort;
        TileSize = Math.Clamp(settings.Current.TileSize ?? layout.TileSizeDefault, layout.TileSizeMin, layout.TileSizeMax);

        if (allowUserSort)
        {
            SortMode = settings.Current.AlbumSortMode;
            SortDescending = settings.Current.AlbumSortDescending;
        }
        else
        {
            SortMode = AlbumSortMode.DateAdded;
            SortDescending = true;
        }

        _library.Tracks.Changed += (_, _) => RebuildAlbums();
        _library.Search.Applied += (_, _) => RebuildRows(force: true);

        RebuildAlbums();
    }

    public void SetViewportWidth(double width)
    {
        if (width == _viewportWidth)
            return;

        _viewportWidth = width;
        RebuildRows(force: false);
    }

    public double TileSizeStep => _layout.TileSizeStep;

    public void AdjustTileSize(double delta) => TileSize = Math.Clamp(TileSize + delta, _layout.TileSizeMin, _layout.TileSizeMax);

    // steps scales the change for a wheel gesture's delta (fractional on touchpads); null means one step.
    [RelayCommand]
    private void IncreaseTileSize(double? steps) => AdjustTileSize((steps ?? 1) * _layout.TileSizeStep);

    [RelayCommand]
    private void DecreaseTileSize(double? steps) => AdjustTileSize(-(steps ?? 1) * _layout.TileSizeStep);

    partial void OnTileSizeChanged(double value)
    {
        _settings.Update(s => s with { TileSize = value });
        RebuildRows(force: false);
    }

    partial void OnSortModeChanged(AlbumSortMode value)
    {
        if (!AllowUserSort)
            return;

        _settings.Update(s => s with { AlbumSortMode = value });
        OnPropertyChanged(nameof(SortModeLabel));
        RebuildRows(force: true);
    }

    partial void OnSortDescendingChanged(bool value)
    {
        if (!AllowUserSort)
            return;

        _settings.Update(s => s with { AlbumSortDescending = value });
        OnPropertyChanged(nameof(SortDirectionSymbol));
        RebuildRows(force: true);
    }

    public string SortDirectionSymbol => SortDescending ? "↓" : "↑";

    public string SortModeLabel => SortMode switch
    {
        AlbumSortMode.Artist => Strings.ColumnArtist,
        AlbumSortMode.Year => Strings.ColumnYear,
        AlbumSortMode.DateAdded => Strings.SortByDateAdded,
        _ => Strings.ColumnTitle,
    };

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

    public event EventHandler<AlbumTrackReveal>? RevealRequested;

    public bool Reveal(string filePath)
    {
        foreach (var album in Rows.SelectMany(r => r.Tiles))
        {
            if (album.TrackRows.FirstOrDefault(r => r.Track.Track.FilePath == filePath) is { } trackRow)
            {
                SetExpandedAlbum(album);
                RevealRequested?.Invoke(this, new AlbumTrackReveal(album, trackRow));
                return true;
            }
        }

        return false;
    }

    private void SetExpandedAlbum(AlbumViewModel? album)
    {
        ExpandedAlbum = album;

        foreach (var row in Rows)
        {
            row.ExpandedAlbum = album is not null && row.Tiles.Contains(album) ? album : null;
        }
    }

    private readonly HashSet<AlbumKey> _staleArtwork = [];

    // A cover edit leaves the Track records equal, so the tracks alone wouldn't get these albums rebuilt.
    public void MarkArtworkStale(IEnumerable<Track> tracks) => _staleArtwork.UnionWith(tracks.Select(AlbumKey.Of));

    public void RebuildIfStale()
    {
        if (_staleArtwork.Count > 0)
            RebuildAlbums();
    }

    // Albums whose tracks didn't change are kept, with their artwork; only new or changed ones are built and loaded.
    private void RebuildAlbums(bool reloadAll = false)
    {
        var existing = reloadAll ? new Dictionary<AlbumKey, AlbumViewModel>() : Albums.ToDictionary(a => AlbumKey.Of(a.UnderlyingTracks[0]));
        foreach (var key in _staleArtwork)
        {
            existing.Remove(key);
        }

        _staleArtwork.Clear();

        // The Recently Added grid shows every album that has one of the newest tracks - the whole album,
        // not just those tracks, so an expanded album is never missing songs.
        var newestPaths = _limitToRecentlyAdded
            ? _library.NewestTracks().Select(t => t.Track.FilePath).ToHashSet()
            : null;

        var groups = _library.Tracks
            .GroupBy(t => AlbumKey.Of(t.Track))
            .Where(g => newestPaths is null || g.Any(t => newestPaths.Contains(t.Track.FilePath)))
            .OrderBy(g => g.Key.Album, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key.AlbumArtist, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var orderedTracks = g
                    .OrderBy(t => t.Track.TrackNumber ?? int.MaxValue)
                    .ThenBy(t => t.Track.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                existing.TryGetValue(g.Key, out var previous);
                if (previous is not null && IsUnchanged(previous, orderedTracks))
                    return (RawKey: $"{g.Key.Album}{g.Key.AlbumArtist}", Album: previous, IsNew: false);

                var album = new AlbumViewModel(
                    g.Key.Album.Length > 0 ? g.Key.Album : Strings.UnknownAlbum,
                    ResolveArtistLabel(orderedTracks),
                    orderedTracks.Select(t => t.Track.Year).FirstOrDefault(y => y.HasValue),
                    orderedTracks,
                    _library)
                {
                    ArtworkBytes = previous?.ArtworkBytes,
                };
                return (RawKey: $"{g.Key.Album}{g.Key.AlbumArtist}", Album: album, IsNew: true);
            })
            .ToList();

        Albums.SyncTo([.. groups.Select(g => g.Album)]);
        RebuildRows(force: true, keepExpanded: true);

        _ = LoadAlbumArtworkAsync([.. groups.Where(g => g.IsNew).Select(g => (g.RawKey, g.Album))]);
    }

    private static bool IsUnchanged(AlbumViewModel album, IReadOnlyList<LibraryTrackViewModel> tracks) =>
        album.Tracks.SequenceEqual(tracks) && album.UnderlyingTracks.SequenceEqual(tracks.Select(t => t.Track));

    private string ArtworkCacheDirectory => ArtworkCacheDirectoryFor(_settings.FilePath);

    public static string ArtworkCacheDirectoryFor(string settingsFilePath) =>
        Path.Combine(Path.GetDirectoryName(settingsFilePath)!, "artwork-cache");

    // Same key the albums are grouped (and their thumbnails cached) by.
    public static string ArtworkKey(Track track)
    {
        var key = AlbumKey.Of(track);
        return $"{key.Album}{key.AlbumArtist}";
    }

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

        RebuildAlbums(reloadAll: true);
    }

    private async Task LoadAlbumArtworkAsync(IReadOnlyList<(string RawKey, AlbumViewModel Album)> groups)
    {
        if (groups.Count == 0)
            return;

        var cacheDirectory = ArtworkCacheDirectory;
        var options = new ParallelOptions { MaxDegreeOfParallelism = ArtworkLoadConcurrency };
        var stopwatch = Stopwatch.StartNew();
        var loaded = new CoalescingDispatch<(AlbumViewModel Album, byte[]? Artwork)>(
            flush => Dispatcher.UIThread.Post(flush, DispatcherPriority.Background),
            batch =>
            {
                foreach (var (album, artwork) in batch)
                {
                    album.ArtworkBytes = artwork;
                }
            });

        await Parallel.ForEachAsync(groups, options, (group, _) =>
        {
            var (rawKey, album) = group;
            var firstTrackPath = album.Tracks.Count > 0 ? album.Tracks[0].Track.FilePath : null;
            if (firstTrackPath is null)
                return ValueTask.CompletedTask;

            try
            {
                loaded.Add((album, AlbumArtworkCache.GetOrCreate(
                    cacheDirectory, rawKey, () => MusicLibraryScanner.LoadArtwork(firstTrackPath))));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load artwork for album {Album}", album.Title);
            }

            return ValueTask.CompletedTask;
        });

        _logger.LogInformation("Loaded artwork for {AlbumCount} albums in {ElapsedMs} ms", groups.Count, stopwatch.ElapsedMilliseconds);
    }

    private static string ResolveArtistLabel(IEnumerable<LibraryTrackViewModel> tracks)
    {
        var artists = tracks.Select(t => t.Track.Artist).ToList();

        // Artists on every track name the album even when some tracks have guests ("Pet Shop Boys" for a
        // track by "Pet Shop Boys" and one by "Pet Shop Boys;Dusty Springfield"); only with nobody in
        // common is it "Various Artists".
        var common = ArtistList.Common(artists);
        if (common.Count > 0)
            return string.Join(Strings.ArtistSeparator, common);

        return artists.Any(a => ArtistList.Split(a).Count > 0) ? Strings.VariousArtists : string.Empty;
    }

    private void RebuildRows(bool force, bool keepExpanded = false)
    {
        var newColumnCount = ComputeColumnCount(_viewportWidth, TileSize);
        if (!force && newColumnCount == _columnCount)
            return;

        _columnCount = newColumnCount;

        var query = _library.Search.Query;
        var albums = !_library.Search.IsActive
            ? SortAlbums(Albums)
            : Albums
                .Select(a => (Album: a, Score: SearchScore(a, query)))
                .Where(x => x.Score is not null)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Album);

        // Rows that hold the same tiles as before are kept, so only the changed ones are re-realized.
        var rows = albums
            .Chunk(_columnCount)
            .Select((tiles, i) => i < Rows.Count && Rows[i].Tiles.SequenceEqual(tiles) ? Rows[i] : new AlbumRowViewModel(tiles))
            .ToList();

        Rows.SyncTo(rows);

        // A library change keeps the expanded album open if it, or the album rebuilt in its place, is still shown.
        var expandedKey = keepExpanded && ExpandedAlbum is { } expanded ? AlbumKey.Of(expanded.UnderlyingTracks[0]) : (AlbumKey?)null;
        SetExpandedAlbum(expandedKey is { } key
            ? rows.SelectMany(r => r.Tiles).FirstOrDefault(a => AlbumKey.Of(a.UnderlyingTracks[0]) == key)
            : null);
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

        AlbumSortMode.DateAdded => SortDescending
            ? albums.OrderByDescending(a => a.DateAdded)
            : albums.OrderBy(a => a.DateAdded),

        _ => SortDescending
            ? albums.OrderByDescending(a => a.Title, StringComparer.OrdinalIgnoreCase)
            : albums.OrderBy(a => a.Title, StringComparer.OrdinalIgnoreCase),
    };

    private int ComputeColumnCount(double viewportWidth, double tileSize)
    {
        if (viewportWidth <= 0)
            return 1;

        return Math.Max(1, (int)Math.Floor((viewportWidth + _layout.TileSpacing) / (tileSize + _layout.TileSpacing)));
    }
}
