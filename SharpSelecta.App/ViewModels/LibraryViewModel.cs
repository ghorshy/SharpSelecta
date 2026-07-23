using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    // CommunityToolkit's generated setters can't be cancelled, so hiding the last visible column
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

        Tracks.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTracks));
            OnPropertyChanged(nameof(NoTracks));
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

    [RelayCommand]
    private Task PlayNowAsync(Track track) => _playbackControls.PlayNowAsync(track);

    [RelayCommand]
    private Task PlayNext(Track track) => _playbackControls.PlayNext(track);

    [RelayCommand]
    private Task AddToQueue(Track track) => _playbackControls.AddToQueue(track);
}
