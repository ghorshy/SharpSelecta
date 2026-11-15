using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed partial class VolumeViewModel : ViewModelBase
{
    private const double Step = 0.05;

    private readonly IAudioEngine _audioEngine;
    private readonly SettingsStore _settings;
    private double? _levelBeforeMute;

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

    partial void OnLevelChanged(double value)
    {
        if (value > 0)
            _levelBeforeMute = null;

        ApplyToEngine();
    }

    partial void OnCurveChanged(VolumeCurve value) => ApplyToEngine();

    private void ApplyToEngine() => _audioEngine.Volume = (float)VolumeScale.ToAmplitude(Level, Curve);

    [RelayCommand]
    private void VolumeUp() => Level = Stepped(_levelBeforeMute ?? Level, Step);

    [RelayCommand]
    private void VolumeDown() => Level = Stepped(Level, -Step);

    private static double Stepped(double level, double delta) => Math.Clamp(Math.Round(level + delta, 2), 0, 1);

    [RelayCommand]
    private void ToggleMute()
    {
        if (_levelBeforeMute is { } restore)
        {
            Level = restore;
        }
        else if (Level > 0)
        {
            var before = Level;
            Level = 0;
            _levelBeforeMute = before;
        }
    }

    public void Persist() => _settings.Update(s => s with { Volume = _levelBeforeMute ?? Level });
}
