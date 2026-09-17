using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
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
    private readonly IFileManagerService _fileManagerService;
    private readonly string _settingsFilePath;
    private readonly ILogger<LibraryViewModel> _logger;

    public string ShowInFileManagerLabel => _fileManagerService.ActionLabel;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }
    public ObservableCollection<string> LibraryFolderPaths { get; } = [];

    public bool HasLibraryFolders => LibraryFolderPaths.Count > 0;

    public ObservableCollection<string> PendingLibraryFolderPaths { get; } = [];

    public bool HasPendingLibraryFolders => PendingLibraryFolderPaths.Count > 0;

    public bool HasPendingChanges => !PendingLibraryFolderPaths.SequenceEqual(LibraryFolderPaths);

    ICommand ISettingsCategoryViewModel.ApplyCommand => ApplyPendingFolderChangesCommand;

    ICommand ISettingsCategoryViewModel.CancelCommand => CancelPendingFolderChangesCommand;

    [ObservableProperty]
    public partial bool IsTrackNumberColumnVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsTitleColumnVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsArtistColumnVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsAlbumColumnVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLengthColumnVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsSampleRateColumnVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBitDepthColumnVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBitrateColumnVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsFileTypeColumnVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsYearColumnVisible { get; set; } = true;

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

    private void SaveColumnVisibility() => SettingsStore.SaveColumnVisibility(
        _settingsFilePath, ColumnVisibilityBindings().ToDictionary(c => c.Key, c => c.Get()));

    private void ApplySavedColumnVisibility()
    {
        var columns = SettingsStore.LoadColumnVisibility(_settingsFilePath);
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

    public BulkObservableCollection<LibraryTrackViewModel> DisplayedTracks { get; } = [];

    private static readonly TimeSpan SearchDebounceDelay = TimeSpan.FromMilliseconds(150);

    private CancellationTokenSource? _searchDebounceCts;

    /// <summary>Completes once a pending debounced search rescan (see OnSearchQueryChanged) has run - for tests.</summary>
    public Task SearchDebounceTask { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = "";

    // Debounce so fast typing doesn't rescan/rescore every track on every keystroke.
    // Clearing is exempt - "" needs no scoring and should feel instant.
    partial void OnSearchQueryChanged(string value)
    {
        _searchDebounceCts?.Cancel();

        if (string.IsNullOrWhiteSpace(value))
        {
            RefreshDisplayedTracks();
            RefreshRecentlyAddedTracks();
            SearchDebounceTask = Task.CompletedTask;
            return;
        }

        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;
        SearchDebounceTask = DebounceRefreshDisplayedTracksAsync(cts.Token);
    }

    private async Task DebounceRefreshDisplayedTracksAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDebounceDelay, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        RefreshDisplayedTracks();
        RefreshRecentlyAddedTracks();
    }

    [RelayCommand]
    private void ClearSearch() => SearchQuery = "";

    public event EventHandler? SearchFocusRequested;

    [RelayCommand]
    private void FocusSearch() => SearchFocusRequested?.Invoke(this, EventArgs.Empty);

    // Column widths/order live in one shared settings file, not per TrackListView instance - this
    // lets the Library and Recently Added grids (two separate control instances, same DataContext)
    // push a layout change to each other immediately instead of only agreeing after a restart.
    public event EventHandler? ColumnLayoutChanged;

    public void NotifyColumnLayoutChanged() => ColumnLayoutChanged?.Invoke(this, EventArgs.Empty);

    private void RefreshDisplayedTracks()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            DisplayedTracks.ReplaceAll(Tracks);
            return;
        }

        var ranked = Tracks
            .Select(t => (Vm: t, Score: FuzzySearch.Score(t.Track, SearchQuery)))
            .Where(x => x.Score is not null)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Vm);

        DisplayedTracks.ReplaceAll(ranked);
    }

    public bool HasTracks => Tracks.Count > 0;

    public bool NoTracks => Tracks.Count == 0;

    [ObservableProperty]
    public partial bool IsLoadingLibrary { get; set; }

    public bool ShowEmptyState => NoTracks && !IsLoadingLibrary;

    partial void OnIsLoadingLibraryChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        NotifyViewVisibilityChanged();
    }

    [ObservableProperty] public partial LibraryViewMode ViewMode { get; set; } = LibraryViewMode.TrackList;

    public LibraryViewMode ActiveViewMode
    {
        get => LibrarySection == LibrarySection.RecentlyAdded ? RecentlyAddedViewMode : ViewMode;
        set
        {
            if (LibrarySection == LibrarySection.RecentlyAdded)
                RecentlyAddedViewMode = value;
            else
                ViewMode = value;
        }
    }

    [ObservableProperty] public partial LibrarySection LibrarySection { get; set; } = LibrarySection.Library;

    [ObservableProperty] public partial LibraryViewMode RecentlyAddedViewMode { get; set; } = LibraryViewMode.TrackList;

    public bool IsTrackListViewVisible => HasTracks && !IsLoadingLibrary && LibrarySection == LibrarySection.Library && ViewMode == LibraryViewMode.TrackList;

    public bool IsAlbumGridViewVisible => HasTracks && !IsLoadingLibrary && LibrarySection == LibrarySection.Library && ViewMode == LibraryViewMode.AlbumGrid;

    public bool IsRecentlyAddedListVisible => HasTracks && !IsLoadingLibrary && LibrarySection == LibrarySection.RecentlyAdded && RecentlyAddedViewMode == LibraryViewMode.TrackList;

    public bool IsRecentlyAddedCoverArtVisible => HasTracks && !IsLoadingLibrary && LibrarySection == LibrarySection.RecentlyAdded && RecentlyAddedViewMode == LibraryViewMode.AlbumGrid;

    // No ViewMode gate here, unlike the other visibility properties - a playlist has no Cover Art mode.
    public bool IsPlaylistViewVisible => HasTracks && !IsLoadingLibrary && LibrarySection == LibrarySection.Playlist;

    private void NotifyViewVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsTrackListViewVisible));
        OnPropertyChanged(nameof(IsAlbumGridViewVisible));
        OnPropertyChanged(nameof(IsRecentlyAddedListVisible));
        OnPropertyChanged(nameof(IsRecentlyAddedCoverArtVisible));
        OnPropertyChanged(nameof(IsPlaylistViewVisible));
        OnPropertyChanged(nameof(ActiveViewMode));
    }

    partial void OnViewModeChanged(LibraryViewMode value)
    {
        SettingsStore.SaveViewMode(_settingsFilePath, value);
        NotifyViewVisibilityChanged();
    }

    partial void OnLibrarySectionChanged(LibrarySection value)
    {
        SettingsStore.SaveLibrarySection(_settingsFilePath, value);
        NotifyViewVisibilityChanged();
    }

    partial void OnRecentlyAddedViewModeChanged(LibraryViewMode value)
    {
        SettingsStore.SaveRecentlyAddedViewMode(_settingsFilePath, value);
        NotifyViewVisibilityChanged();
    }

    [RelayCommand]
    private void SetViewMode(LibraryViewMode mode) => ViewMode = mode;

    [RelayCommand]
    private void SetLibrarySection(LibrarySection section) => LibrarySection = section;

    public BulkObservableCollection<LibraryTrackViewModel> RecentlyAddedTracks { get; } = [];

    private void RefreshRecentlyAddedTracks()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            RecentlyAddedTracks.ReplaceAll(Tracks.OrderByDescending(t => t.Track.DateAddedUtc));
            return;
        }

        var matching = Tracks
            .Where(t => FuzzySearch.Score(t.Track, SearchQuery) is not null)
            .OrderByDescending(t => t.Track.DateAddedUtc);

        RecentlyAddedTracks.ReplaceAll(matching);
    }

    public AlbumGridViewModel Grid { get; }

    public AlbumGridViewModel RecentlyAddedGrid { get; }

    public PlaylistsViewModel Playlists { get; }

    public string SettingsFilePath => _settingsFilePath;

    public LibraryViewModel(
        IFilePickerService filePickerService,
        PlaybackControlsViewModel playbackControls,
        IFileManagerService fileManagerService,
        string settingsFilePath,
        ILogger<LibraryViewModel> logger)
    {
        _filePickerService = filePickerService;
        _playbackControls = playbackControls;
        _fileManagerService = fileManagerService;
        _settingsFilePath = settingsFilePath;
        _logger = logger;

        Grid = new AlbumGridViewModel(this, settingsFilePath, _logger);
        RecentlyAddedGrid = new AlbumGridViewModel(this, settingsFilePath, _logger, allowUserSort: false);
        Playlists = new PlaylistsViewModel(this, settingsFilePath);

        Tracks.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTracks));
            OnPropertyChanged(nameof(NoTracks));
            OnPropertyChanged(nameof(ShowEmptyState));
            NotifyViewVisibilityChanged();
            RefreshDisplayedTracks();
            RefreshRecentlyAddedTracks();
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

    public async Task InitializeAsync()
    {
        ApplySavedColumnVisibility();
        ViewMode = SettingsStore.LoadViewMode(_settingsFilePath) ?? LibraryViewMode.TrackList;
        LibrarySection = SettingsStore.LoadLibrarySection(_settingsFilePath) ?? LibrarySection.Library;
        RecentlyAddedViewMode = SettingsStore.LoadRecentlyAddedViewMode(_settingsFilePath) ?? LibraryViewMode.TrackList;

        var folderPaths = SettingsStore.LoadLibraryFolderPaths(_settingsFilePath);
        if (folderPaths is not null)
        {
            foreach (var folderPath in folderPaths)
            {
                LibraryFolderPaths.Add(folderPath);
            }

            var hydrateStopwatch = Stopwatch.StartNew();
            var hydrated = await Task.Run(() => LibraryIndexStore.LoadIndexed(_settingsFilePath, folderPaths));
            if (hydrated.Count > 0)
            {
                _logger.LogInformation("Hydrated {TrackCount} tracks from index in {ElapsedMs} ms", hydrated.Count, hydrateStopwatch.ElapsedMilliseconds);
                Tracks.ReplaceAll(hydrated.Select(track => new LibraryTrackViewModel(track, this)));
            }

            await ReconcileFoldersAsync();
        }
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folderPath = await _filePickerService.PickLibraryFolderAsync();
        if (folderPath is null || LibraryFolderPaths.Contains(folderPath))
            return;

        LibraryFolderPaths.Add(folderPath);
        SettingsStore.SaveLibraryFolderPaths(_settingsFilePath, LibraryFolderPaths);
        await ReconcileFoldersAsync();
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
        if (!HasPendingChanges)
            return;

        // ToList() snapshots before Clear() re-syncs PendingLibraryFolderPaths as a side effect.
        var folderPaths = PendingLibraryFolderPaths.ToList();

        LibraryFolderPaths.Clear();
        foreach (var folderPath in folderPaths)
        {
            LibraryFolderPaths.Add(folderPath);
        }

        SettingsStore.SaveLibraryFolderPaths(_settingsFilePath, LibraryFolderPaths);
        await ReconcileFoldersAsync();
    }

    [RelayCommand]
    private void CancelPendingFolderChanges() => SyncPendingLibraryFolderPaths();

    [RelayCommand]
    private Task RescanAsync() => ReconcileFoldersAsync();

    private async Task ReconcileFoldersAsync()
    {
        if (LibraryFolderPaths.Count == 0)
        {
            Tracks.ReplaceAll([]);
            StatusMessage = null;
            return;
        }

        var folderPaths = LibraryFolderPaths.ToList();
        var showSpinner = Tracks.Count == 0;
        if (showSpinner)
        {
            IsLoadingLibrary = true;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await Task.Run(() => LibraryIndexStore.Reconcile(_settingsFilePath, folderPaths));

            _logger.LogInformation("Reconciled {TrackCount} tracks in {ElapsedMs} ms", result.Tracks.Count, stopwatch.ElapsedMilliseconds);
            Tracks.ReplaceAll(result.Tracks.Select(track => new LibraryTrackViewModel(track, this)));
            StatusMessage = result.FailedFolders.Count > 0
                ? Strings.FailedToScanFolder(string.Join(", ", result.FailedFolders))
                : null;
        }
        finally
        {
            if (showSpinner)
            {
                IsLoadingLibrary = false;
            }
        }
    }

    [RelayCommand]
    private Task PlayNowAsync(Track track) => _playbackControls.PlayNowAsync(track);

    [RelayCommand]
    private Task PlayNext(Track track) => _playbackControls.PlayNext(ResolveSelection(track));

    [RelayCommand]
    private Task AddToQueue(Track track) => _playbackControls.AddToQueue(ResolveSelection(track));

    private IReadOnlyList<Track> _selectedTracksInOrder = [];

    // Pushed by the view on every DataGrid selection change - DataGrid.SelectedItems doesn't
    // preserve click order, so the view tracks it there and hands the ordered list here.
    public void SetSelectedTracksInOrder(IReadOnlyList<Track> tracks) => _selectedTracksInOrder = tracks;

    // A right-click inside the current multi-selection acts on the whole (ordered) selection;
    // a right-click outside it acts on just the clicked track, like most file browsers.
    private IReadOnlyList<Track> ResolveSelection(Track clickedTrack) =>
        _selectedTracksInOrder.Contains(clickedTrack) ? _selectedTracksInOrder : [clickedTrack];

    [RelayCommand]
    private Task PlayAlbumNowAsync(AlbumViewModel album) => _playbackControls.PlayNowAsync(album.UnderlyingTracks);

    [RelayCommand]
    private Task PlayAlbumNext(AlbumViewModel album) => _playbackControls.PlayNext(album.UnderlyingTracks);

    [RelayCommand]
    private Task AddAlbumToQueue(AlbumViewModel album) => _playbackControls.AddToQueue(album.UnderlyingTracks);

    [RelayCommand]
    private Task ShowInFileManagerAsync(Track track) => _fileManagerService.RevealInFileManagerAsync(track.FilePath);

    [RelayCommand]
    private Task ShowAlbumInFileManagerAsync(AlbumViewModel album) => album.UnderlyingTracks.Count > 0
        ? _fileManagerService.RevealInFileManagerAsync(album.UnderlyingTracks[0].FilePath)
        : Task.CompletedTask;
}
