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
    private readonly PlaybackQueue _queue;
    private readonly ILogger<LibraryViewModel> _logger;

    [ObservableProperty]
    private string? statusMessage;

    public ObservableCollection<LibraryTrackViewModel> Tracks { get; } = [];

    public LibraryViewModel(
        IFilePickerService filePickerService,
        PlaybackControlsViewModel playbackControls,
        PlaybackQueue queue,
        ILogger<LibraryViewModel> logger)
    {
        _filePickerService = filePickerService;
        _playbackControls = playbackControls;
        _queue = queue;
        _logger = logger;
    }

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        var folderPath = await _filePickerService.PickLibraryFolderAsync();
        if (folderPath is null)
            return;

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
    private void PlayNext(Track track) => _queue.PlayNext(track);

    [RelayCommand]
    private void AddToQueue(Track track) => _queue.AddToQueue(track);
}
