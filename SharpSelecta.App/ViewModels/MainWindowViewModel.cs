using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAudioEngine _audioEngine;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayFileName))]
    private string? loadedFileName;

    [ObservableProperty]
    private string? statusMessage;

    public PlaybackControlsViewModel PlaybackControls { get; }

    public MainWindowViewModel(IAudioEngine audioEngine, IFilePickerService filePickerService, ILogger<MainWindowViewModel> logger)
    {
        _audioEngine = audioEngine;
        _filePickerService = filePickerService;
        _logger = logger;
        PlaybackControls = new PlaybackControlsViewModel(audioEngine);
    }

    public string DisplayFileName => LoadedFileName ?? Strings.NoFileLoaded;

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var filePath = await _filePickerService.PickAudioFileAsync();
        if (filePath is null)
            return;

        try
        {
            await Task.Run(() => _audioEngine.Load(filePath));
            LoadedFileName = Path.GetFileName(filePath);
            StatusMessage = null;
            PlaybackControls.IsPlaying = false;
            PlaybackControls.RefreshPosition();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load {FilePath}", filePath);
            StatusMessage = Strings.FailedToLoadFile(ex.Message);
        }
    }
}
