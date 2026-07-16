using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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

    public ObservableCollection<LibraryTrackViewModel> Tracks { get; } = [];

    public bool HasTracks => Tracks.Count > 0;

    public bool NoTracks => Tracks.Count == 0;

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
    }

    // Re-scans whatever library folder was remembered from a previous session, if any.
    public async Task InitializeAsync()
    {
        var folderPath = LibrarySettingsStore.LoadLibraryFolderPath(_settingsFilePath);
        if (folderPath is not null)
        {
            await LoadFolderAsync(folderPath);
        }
    }

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        var folderPath = await _filePickerService.PickLibraryFolderAsync();
        if (folderPath is null)
            return;

        LibrarySettingsStore.SaveLibraryFolderPath(_settingsFilePath, folderPath);
        await LoadFolderAsync(folderPath);
    }

    private async Task LoadFolderAsync(string folderPath)
    {
        try
        {
            var tracks = await Task.Run(() => MusicLibraryScanner.Scan(folderPath));
            Tracks.Clear();
            foreach (var track in tracks)
            {
                Tracks.Add(new LibraryTrackViewModel(track, this));
            }

            StatusMessage = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan library folder {FolderPath}", folderPath);
            StatusMessage = Strings.FailedToScanFolder(ex.Message);
        }
    }

    [RelayCommand]
    private Task PlayNowAsync(Track track) => _playbackControls.PlayNowAsync(track);

    [RelayCommand]
    private void PlayNext(Track track) => _playbackControls.PlayNext(track);

    [RelayCommand]
    private void AddToQueue(Track track) => _playbackControls.AddToQueue(track);
}
