using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed partial class WaveformViewModel : ViewModelBase
{
    // WaveformSliderView downsamples this to however many bars fit its width.
    private const int MasterPointCount = 2000;

    private readonly IAudioEngine _audioEngine;
    private readonly SettingsStore _settings;
    private readonly ILogger _logger;
    private int _loadGeneration;

    [ObservableProperty]
    public partial IReadOnlyList<float> Peaks { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Exposed for tests.</summary>
    public Task LoadTask { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    public partial bool UseSlider { get; set; }

    public WaveformViewModel(IAudioEngine audioEngine, SettingsStore settings, ILogger logger)
    {
        _audioEngine = audioEngine;
        _settings = settings;
        _logger = logger;
        UseSlider = settings.Current.UseWaveformSlider;
    }

    // Extracts from whatever the engine has loaded; the track is the cache key.
    public void LoadForCurrentTrack(Track track)
    {
        Peaks = [];
        IsLoading = true;
        LoadTask = LoadAsync(track, ++_loadGeneration);
    }

    private async Task LoadAsync(Track track, int generation)
    {
        try
        {
            var peaks = await Task.Run(() =>
            {
                var cached = LibraryIndexStore.TryGetWaveformPeaks(_settings.FilePath, track.FilePath);
                if (cached is not null)
                {
                    return cached;
                }

                var extracted = _audioEngine.GetWaveformPeaks(MasterPointCount);
                LibraryIndexStore.SaveWaveformPeaks(_settings.FilePath, track.FilePath, extracted);
                return extracted;
            });

            if (generation == _loadGeneration)
            {
                Peaks = peaks;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract waveform peaks for {FilePath}", track.FilePath);
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsLoading = false;
            }
        }
    }
}
