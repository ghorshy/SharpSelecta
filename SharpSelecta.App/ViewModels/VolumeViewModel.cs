using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed partial class VolumeViewModel : ViewModelBase
{
    private readonly IAudioEngine _audioEngine;
    private readonly SettingsStore _settings;

    [ObservableProperty]
    public partial double Level { get; set; } = 1.0;

    [ObservableProperty]
    public partial VolumeCurve Curve { get; set; } = VolumeCurve.Linear;

    public VolumeViewModel(IAudioEngine audioEngine, SettingsStore settings)
    {
        _audioEngine = audioEngine;
        _settings = settings;

        Curve = settings.Current.VolumeCurve;
        Level = settings.Current.Volume ?? Level;
    }

    partial void OnLevelChanged(double value) => ApplyToEngine();

    partial void OnCurveChanged(VolumeCurve value) => ApplyToEngine();

    private void ApplyToEngine() => _audioEngine.Volume = (float)VolumeScale.ToAmplitude(Level, Curve);

    public void Persist() => _settings.Update(s => s with { Volume = Level });
}
