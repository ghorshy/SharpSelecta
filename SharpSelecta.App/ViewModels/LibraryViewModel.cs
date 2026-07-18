using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Collections;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly IFilePickerService _filePickerService;
    private readonly PlaybackControlsViewModel _playbackControls;
    private readonly string _settingsFilePath;
    private readonly ILogger<LibraryViewModel> _logger;

    [ObservableProperty]
    private string? statusMessage;

    public ObservableCollection<string> LibraryFolderPaths { get; } = [];

    public bool HasLibraryFolders => LibraryFolderPaths.Count > 0;

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

    // CommunityToolkit's generated setters can't be cancelled, so hiding the last visible column
    // is undone here instead of blocked up front.
    private bool AnyColumnVisible() =>
        IsTrackNumberColumnVisible || IsTitleColumnVisible || IsArtistColumnVisible || IsAlbumColumnVisible ||
        IsLengthColumnVisible || IsSampleRateColumnVisible || IsBitDepthColumnVisible || IsBitrateColumnVisible ||
        IsFileTypeColumnVisible || IsYearColumnVisible;

    partial void OnIsTrackNumberColumnVisibleChanged(bool value)
    {
        if (!value && !AnyColumnVisible()) { IsTrackNumberColumnVisible = true; return; }
        SaveColumnVisibility();
    }

    partial void OnIsTitleColumnVisibleChanged(bool value)
    {
        if (!value && !AnyColumnVisible()) { IsTitleColumnVisible = true; return; }
        SaveColumnVisibility();
    }

    partial void OnIsArtistColumnVisibleChanged(bool value)
    {
        if (!value && !AnyColumnVisible()) { IsArtistColumnVisible = true; return; }
        SaveColumnVisibility();
    }

    partial void OnIsAlbumColumnVisibleChanged(bool value)
    {
        if (!value && !AnyColumnVisible()) { IsAlbumColumnVisible = true; return; }
        SaveColumnVisibility();
    }

    partial void OnIsLengthColumnVisibleChanged(bool value)
    {
        if (!value && !AnyColumnVisible()) { IsLengthColumnVisible = true; return; }
        SaveColumnVisibility();
    }

    partial void OnIsSampleRateColumnVisibleChanged(bool value)
    {
        if (!value && !AnyColumnVisible()) { IsSampleRateColumnVisible = true; return; }
        SaveColumnVisibility();
    }

    partial void OnIsBitDepthColumnVisibleChanged(bool value)
    {
        if (!value && !AnyColumnVisible()) { IsBitDepthColumnVisible = true; return; }
        SaveColumnVisibility();
    }

    partial void OnIsBitrateColumnVisibleChanged(bool value)
    {
        if (!value && !AnyColumnVisible()) { IsBitrateColumnVisible = true; return; }
        SaveColumnVisibility();
    }

    partial void OnIsFileTypeColumnVisibleChanged(bool value)
    {
        if (!value && !AnyColumnVisible()) { IsFileTypeColumnVisible = true; return; }
        SaveColumnVisibility();
    }

    partial void OnIsYearColumnVisibleChanged(bool value)
    {
        if (!value && !AnyColumnVisible()) { IsYearColumnVisible = true; return; }
        SaveColumnVisibility();
    }

    private void SaveColumnVisibility() => LibrarySettingsStore.SaveColumnVisibility(_settingsFilePath, new ColumnVisibility(
        IsTrackNumberColumnVisible, IsTitleColumnVisible, IsArtistColumnVisible, IsAlbumColumnVisible,
        IsLengthColumnVisible, IsSampleRateColumnVisible, IsBitDepthColumnVisible, IsBitrateColumnVisible,
        IsFileTypeColumnVisible, IsYearColumnVisible));

    private void ApplySavedColumnVisibility()
    {
        var columns = LibrarySettingsStore.LoadColumnVisibility(_settingsFilePath);
        if (columns is null)
            return;

        IsTrackNumberColumnVisible = columns.TrackNumber;
        IsTitleColumnVisible = columns.Title;
        IsArtistColumnVisible = columns.Artist;
        IsAlbumColumnVisible = columns.Album;
        IsLengthColumnVisible = columns.Length;
        IsSampleRateColumnVisible = columns.SampleRate;
        IsBitDepthColumnVisible = columns.BitDepth;
        IsBitrateColumnVisible = columns.Bitrate;
        IsFileTypeColumnVisible = columns.FileType;
        IsYearColumnVisible = columns.Year;
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

        LibraryFolderPaths.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasLibraryFolders));
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
    private async Task RemoveFolderAsync(string folderPath)
    {
        LibraryFolderPaths.Remove(folderPath);
        LibrarySettingsStore.SaveLibraryFolderPaths(_settingsFilePath, LibraryFolderPaths);
        await LoadFoldersAsync();
    }

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
