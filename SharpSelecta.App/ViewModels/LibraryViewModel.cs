using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Collections;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class LibraryViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    private readonly IFilePickerService _filePickerService;
    private readonly PlaybackControlsViewModel _playbackControls;
    private readonly string _settingsFilePath;
    private readonly ILogger<LibraryViewModel> _logger;

    [ObservableProperty]
    private string? statusMessage;

    public ObservableCollection<string> LibraryFolderPaths { get; } = [];

    public bool HasLibraryFolders => LibraryFolderPaths.Count > 0;

    // Settings edits this working copy; nothing touches LibraryFolderPaths (and triggers a
    // rescan) until Apply is confirmed. Cancel just resets it back to the applied list.
    public ObservableCollection<string> PendingLibraryFolderPaths { get; } = [];

    public bool HasPendingLibraryFolders => PendingLibraryFolderPaths.Count > 0;

    public bool HasPendingChanges => !PendingLibraryFolderPaths.SequenceEqual(LibraryFolderPaths);

    ICommand ISettingsCategoryViewModel.ApplyCommand => ApplyPendingFolderChangesCommand;

    ICommand ISettingsCategoryViewModel.CancelCommand => CancelPendingFolderChangesCommand;

    // Toggled from the column header's right-click menu; all visible by default.
    [ObservableProperty]
    private bool isTrackNumberColumnVisible = true;

    [ObservableProperty]
    private bool isTitleColumnVisible = true;

    [ObservableProperty]
    private bool isArtistColumnVisible = true;

    [ObservableProperty]
    private bool isAlbumColumnVisible = true;

    [ObservableProperty]
    private bool isLengthColumnVisible = true;

    [ObservableProperty]
    private bool isSampleRateColumnVisible = true;

    [ObservableProperty]
    private bool isBitDepthColumnVisible = true;

    [ObservableProperty]
    private bool isBitrateColumnVisible = true;

    [ObservableProperty]
    private bool isFileTypeColumnVisible = true;

    [ObservableProperty]
    private bool isYearColumnVisible = true;

    private IEnumerable<(string Key, Func<bool> Get, Action<bool> Set)> ColumnVisibilityBindings() =>
    [
        ("TrackNumber", () => IsTrackNumberColumnVisible, v => IsTrackNumberColumnVisible = v),
        ("Title", () => IsTitleColumnVisible, v => IsTitleColumnVisible = v),
        ("Artist", () => IsArtistColumnVisible, v => IsArtistColumnVisible = v),
        ("Album", () => IsAlbumColumnVisible, v => IsAlbumColumnVisible = v),
        ("Length", () => IsLengthColumnVisible, v => IsLengthColumnVisible = v),
        ("SampleRate", () => IsSampleRateColumnVisible, v => IsSampleRateColumnVisible = v),
        ("BitDepth", () => IsBitDepthColumnVisible, v => IsBitDepthColumnVisible = v),
        ("Bitrate", () => IsBitrateColumnVisible, v => IsBitrateColumnVisible = v),
        ("FileType", () => IsFileTypeColumnVisible, v => IsFileTypeColumnVisible = v),
        ("Year", () => IsYearColumnVisible, v => IsYearColumnVisible = v),
    ];

    // CommunityToolkit's generated setters can't be canceled, so hiding the last visible column
    // is undone here instead of blocked up front.
    private bool AnyColumnVisible() => ColumnVisibilityBindings().Any(c => c.Get());

    private void OnColumnVisibilityChanged(bool value, Action<bool> revert)
    {
        if (!value && !AnyColumnVisible()) { revert(true); return; }
        SaveColumnVisibility();
    }

    partial void OnIsTrackNumberColumnVisibleChanged(bool value) => OnColumnVisibilityChanged(value, v => IsTrackNumberColumnVisible = v);

    partial void OnIsTitleColumnVisibleChanged(bool value) => OnColumnVisibilityChanged(value, v => IsTitleColumnVisible = v);

    partial void OnIsArtistColumnVisibleChanged(bool value) => OnColumnVisibilityChanged(value, v => IsArtistColumnVisible = v);

    partial void OnIsAlbumColumnVisibleChanged(bool value) => OnColumnVisibilityChanged(value, v => IsAlbumColumnVisible = v);

    partial void OnIsLengthColumnVisibleChanged(bool value) => OnColumnVisibilityChanged(value, v => IsLengthColumnVisible = v);

    partial void OnIsSampleRateColumnVisibleChanged(bool value) => OnColumnVisibilityChanged(value, v => IsSampleRateColumnVisible = v);

    partial void OnIsBitDepthColumnVisibleChanged(bool value) => OnColumnVisibilityChanged(value, v => IsBitDepthColumnVisible = v);

    partial void OnIsBitrateColumnVisibleChanged(bool value) => OnColumnVisibilityChanged(value, v => IsBitrateColumnVisible = v);

    partial void OnIsFileTypeColumnVisibleChanged(bool value) => OnColumnVisibilityChanged(value, v => IsFileTypeColumnVisible = v);

    partial void OnIsYearColumnVisibleChanged(bool value) => OnColumnVisibilityChanged(value, v => IsYearColumnVisible = v);

    private void SaveColumnVisibility() => LibrarySettingsStore.SaveColumnVisibility(
        _settingsFilePath, ColumnVisibilityBindings().ToDictionary(c => c.Key, c => c.Get()));

    private void ApplySavedColumnVisibility()
    {
        var columns = LibrarySettingsStore.LoadColumnVisibility(_settingsFilePath);
        if (columns is null)
            return;

        foreach (var (key, _, set) in ColumnVisibilityBindings())
        {
            if (columns.TryGetValue(key, out var visible))
            {
                set(visible);
            }
        }
    }

    public BulkObservableCollection<LibraryTrackViewModel> Tracks { get; } = [];

    public bool HasTracks => Tracks.Count > 0;

    public bool NoTracks => Tracks.Count == 0;

    [ObservableProperty]
    private bool isLoadingLibrary;

    // NoTracks alone can't distinguish "nothing configured yet" from "a scan of already-configured
    // folders is in progress" — without this, the empty-state "Add Folder" button would flash up
    // misleadingly on every app startup while the remembered folders are still being (re)scanned.
    public bool ShowEmptyState => NoTracks && !IsLoadingLibrary;

    partial void OnIsLoadingLibraryChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));

    [ObservableProperty]
    private LibraryViewMode viewMode = LibraryViewMode.TrackList;

    public bool IsTrackListViewVisible => HasTracks && ViewMode == LibraryViewMode.TrackList;

    public bool IsAlbumGridViewVisible => HasTracks && ViewMode == LibraryViewMode.AlbumGrid;

    partial void OnViewModeChanged(LibraryViewMode value)
    {
        LibrarySettingsStore.SaveViewMode(_settingsFilePath, value);
        OnPropertyChanged(nameof(IsTrackListViewVisible));
        OnPropertyChanged(nameof(IsAlbumGridViewVisible));
    }

    [RelayCommand]
    private void SetViewMode(LibraryViewMode mode) => ViewMode = mode;

    // The album grid view's data source — grouped from Tracks (not scanned independently), keyed
    // by a trimmed, case-insensitive Album title so tag whitespace/casing differences don't fork
    // one album into two tiles. Various-artist compilations intentionally collapse into a single
    // tile (grouped by Album alone, not Artist+Album).
    public BulkObservableCollection<AlbumViewModel> Albums { get; } = [];

    public AlbumGridViewModel Grid { get; }

    private void RebuildAlbums()
    {
        // The raw (trimmed, original-case) group key is kept alongside each AlbumViewModel so the
        // artwork cache can be keyed off it directly — using the localized "Unknown Album" display
        // fallback as a cache key would tie cache filenames to the current UI language.
        var groups = Tracks
            .GroupBy(t => (t.Track.Album ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var orderedTracks = g.OrderBy(t => t.Track.TrackNumber ?? int.MaxValue).ToList();
                var album = new AlbumViewModel(
                    g.Key.Length > 0 ? g.Key : Strings.UnknownAlbum,
                    ResolveArtistLabel(orderedTracks),
                    orderedTracks,
                    this);
                return (RawKey: g.Key, Album: album);
            })
            .ToList();

        Albums.ReplaceAll(groups.Select(g => g.Album));

        _ = LoadAlbumArtworkAsync(groups);
    }

    // Bounded parallelism (half the cores): sequential was far too slow on a cold cache (minutes
    // for a large library), but running fully unbounded would peg every core decoding/resizing
    // cover art at once, right back to the CPU contention with the real-time audio thread that
    // this whole perf pass was fixing in the first place.
    private static readonly int ArtworkLoadConcurrency = Math.Max(1, Environment.ProcessorCount / 2);

    private string ArtworkCacheDirectory => Path.Combine(Path.GetDirectoryName(_settingsFilePath)!, "artwork-cache");

    [RelayCommand]
    private void ClearArtworkCache()
    {
        if (Directory.Exists(ArtworkCacheDirectory))
        {
            // Deleted file-by-file rather than Directory.Delete(recursive: true) — a
            // LoadAlbumArtworkAsync pass from an earlier rescan/startup may still be writing new
            // thumbnails into this same directory concurrently, and a recursive directory delete
            // throws IOException ("Directory not empty") if a writer adds a file between its
            // internal enumeration and the final directory removal. Deleting files individually
            // (and tolerating one being mid-write) avoids that race entirely.
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

        // Re-groups from the already-scanned Tracks (no rescan needed) and kicks off a fresh
        // LoadAlbumArtworkAsync pass, which regenerates every thumbnail since the disk cache it
        // would otherwise have hit was just deleted.
        RebuildAlbums();
    }

    private async Task LoadAlbumArtworkAsync(IReadOnlyList<(string RawKey, AlbumViewModel Album)> groups)
    {
        var cacheDirectory = ArtworkCacheDirectory;
        var options = new ParallelOptions { MaxDegreeOfParallelism = ArtworkLoadConcurrency };

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

                // GetOrCreate above runs on this Parallel.ForEachAsync worker thread, not the UI
                // thread — ArtworkBytes is an ObservableProperty, so its change notification has
                // to be raised back on the UI thread for bindings to update safely.
                await Dispatcher.UIThread.InvokeAsync(() => album.ArtworkBytes = artwork);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load artwork for album {Album}", album.Title);
            }
        });
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

    // Column order isn't modeled as a ViewModel property like visibility is — DataGridColumn's
    // DisplayIndex lives on the control itself — so LibraryView reads/writes it directly through
    // LibrarySettingsStore using this path.
    public string SettingsFilePath => _settingsFilePath;

    public LibraryViewModel(
        IFilePickerService filePickerService,
        PlaybackControlsViewModel playbackControls,
        string settingsFilePath,
        ILogger<LibraryViewModel> logger)
    {
        _filePickerService = filePickerService;
        _playbackControls = playbackControls;
        _settingsFilePath = settingsFilePath;
        _logger = logger;

        Grid = new AlbumGridViewModel(this, settingsFilePath);

        Tracks.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTracks));
            OnPropertyChanged(nameof(NoTracks));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(IsTrackListViewVisible));
            OnPropertyChanged(nameof(IsAlbumGridViewVisible));
            RebuildAlbums();
        };

        LibraryFolderPaths.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasLibraryFolders));
            OnPropertyChanged(nameof(HasPendingChanges));
            SyncPendingLibraryFolderPaths();
        };

        PendingLibraryFolderPaths.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPendingLibraryFolders));
            OnPropertyChanged(nameof(HasPendingChanges));
        };
    }

    private void SyncPendingLibraryFolderPaths()
    {
        PendingLibraryFolderPaths.Clear();
        foreach (var folderPath in LibraryFolderPaths)
        {
            PendingLibraryFolderPaths.Add(folderPath);
        }
    }

    // Re-scans whatever library folders were remembered from a previous session, if any.
    public async Task InitializeAsync()
    {
        ApplySavedColumnVisibility();
        ViewMode = LibrarySettingsStore.LoadViewMode(_settingsFilePath) ?? LibraryViewMode.TrackList;

        var folderPaths = LibrarySettingsStore.LoadLibraryFolderPaths(_settingsFilePath);
        if (folderPaths is not null)
        {
            foreach (var folderPath in folderPaths)
            {
                LibraryFolderPaths.Add(folderPath);
            }

            await LoadFoldersAsync();
        }
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folderPath = await _filePickerService.PickLibraryFolderAsync();
        if (folderPath is null || LibraryFolderPaths.Contains(folderPath))
            return;

        LibraryFolderPaths.Add(folderPath);
        LibrarySettingsStore.SaveLibraryFolderPaths(_settingsFilePath, LibraryFolderPaths);
        await LoadFoldersAsync();
    }

    [RelayCommand]
    private async Task AddPendingFolderAsync()
    {
        var folderPath = await _filePickerService.PickLibraryFolderAsync();
        if (folderPath is null || PendingLibraryFolderPaths.Contains(folderPath))
            return;

        PendingLibraryFolderPaths.Add(folderPath);
    }

    [RelayCommand]
    private void RemovePendingFolder(string folderPath) => PendingLibraryFolderPaths.Remove(folderPath);

    [RelayCommand]
    private async Task ApplyPendingFolderChangesAsync()
    {
        // Snapshotted first: clearing LibraryFolderPaths below re-syncs PendingLibraryFolderPaths
        // as a side effect, which would otherwise wipe out the list being enumerated here.
        var folderPaths = PendingLibraryFolderPaths.ToList();

        LibraryFolderPaths.Clear();
        foreach (var folderPath in folderPaths)
        {
            LibraryFolderPaths.Add(folderPath);
        }

        LibrarySettingsStore.SaveLibraryFolderPaths(_settingsFilePath, LibraryFolderPaths);
        await LoadFoldersAsync();
    }

    [RelayCommand]
    private void CancelPendingFolderChanges() => SyncPendingLibraryFolderPaths();

    // Manual refresh entry point (e.g. an F5 action) — re-scans the currently configured folders
    // from scratch, picking up tag/file changes without requiring a folder add/remove round trip.
    [RelayCommand]
    private Task RescanAsync() => LoadFoldersAsync();

    private async Task LoadFoldersAsync()
    {
        if (LibraryFolderPaths.Count == 0)
        {
            Tracks.ReplaceAll([]);
            StatusMessage = null;
            return;
        }

        var folderPaths = LibraryFolderPaths.ToList();
        var failedFolders = new List<string>();

        IsLoadingLibrary = true;
        try
        {
            var tracks = await Task.Run(() =>
            {
                var scanned = new List<Track>();
                foreach (var folderPath in folderPaths)
                {
                    try
                    {
                        scanned.AddRange(MusicLibraryScanner.Scan(folderPath));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to scan library folder {FolderPath}", folderPath);
                        failedFolders.Add(folderPath);
                    }
                }

                return scanned.DistinctBy(t => t.FilePath).ToList();
            });

            Tracks.ReplaceAll(tracks.Select(track => new LibraryTrackViewModel(track, this)));
            StatusMessage = failedFolders.Count > 0 ? Strings.FailedToScanFolder(string.Join(", ", failedFolders)) : null;
        }
        finally
        {
            IsLoadingLibrary = false;
        }
    }

    [RelayCommand]
    private Task PlayNowAsync(Track track) => _playbackControls.PlayNowAsync(track);

    [RelayCommand]
    private Task PlayNext(Track track) => _playbackControls.PlayNext(track);

    [RelayCommand]
    private Task AddToQueue(Track track) => _playbackControls.AddToQueue(track);

    // Whole-album actions (right-click a tile in the album grid) — same commands as the
    // per-track ones above, just applied to every track in the album, in TrackNumber order.
    [RelayCommand]
    private Task PlayAlbumNowAsync(AlbumViewModel album) => _playbackControls.PlayNowAsync(album.Tracks.Select(t => t.Track).ToList());

    [RelayCommand]
    private Task PlayAlbumNext(AlbumViewModel album) => _playbackControls.PlayNext(album.Tracks.Select(t => t.Track).ToList());

    [RelayCommand]
    private Task AddAlbumToQueue(AlbumViewModel album) => _playbackControls.AddToQueue(album.Tracks.Select(t => t.Track).ToList());
}
