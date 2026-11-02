using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class PlaybackSettingsViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    private readonly SettingsStore _settings;
    private readonly IOutputDeviceService _outputDeviceService;
    private readonly PlaybackControlsViewModel _playbackControls;

    // Last-applied snapshot of each setting - HasPendingChanges compares the live (edited)
    // property values against these, and Cancel reverts to them.
    private bool _appliedRestoreQueueOnStartup;
    private string _appliedSelectedOutputDeviceDisplayName;
    private bool _appliedUseLogarithmicVolumeScale;
    private int _appliedSeekStepSeconds;
    private bool _appliedUseWaveformSlider;

    public ObservableCollection<string> OutputDeviceDisplayNames { get; } = [Strings.SystemDefaultAudioDevice];

    [ObservableProperty]
    public partial bool RestoreQueueOnStartup { get; set; }

    [ObservableProperty]
    private string selectedOutputDeviceDisplayName = Strings.SystemDefaultAudioDevice;

    [ObservableProperty]
    private bool useLogarithmicVolumeScale;

    [ObservableProperty]
    private int seekStepSeconds = 5;

    [ObservableProperty]
    private bool useWaveformSlider;

    public bool HasPendingChanges =>
        RestoreQueueOnStartup != _appliedRestoreQueueOnStartup ||
        SelectedOutputDeviceDisplayName != _appliedSelectedOutputDeviceDisplayName ||
        UseLogarithmicVolumeScale != _appliedUseLogarithmicVolumeScale ||
        SeekStepSeconds != _appliedSeekStepSeconds ||
        UseWaveformSlider != _appliedUseWaveformSlider;

    ICommand ISettingsCategoryViewModel.ApplyCommand => ApplyPlaybackSettingsCommand;

    ICommand ISettingsCategoryViewModel.CancelCommand => CancelPlaybackSettingsCommand;

    public PlaybackSettingsViewModel(SettingsStore settings, IOutputDeviceService outputDeviceService, PlaybackControlsViewModel playbackControls)
    {
        _settings = settings;
        var saved = settings.Current;
        _outputDeviceService = outputDeviceService;
        _playbackControls = playbackControls;

        RestoreQueueOnStartup = saved.RestoreQueueOnStartup;
        _appliedRestoreQueueOnStartup = RestoreQueueOnStartup;

        useWaveformSlider = saved.UseWaveformSlider;
        _playbackControls.UseWaveformSlider = useWaveformSlider;
        _appliedUseWaveformSlider = useWaveformSlider;

        if (saved.OutputDeviceName is { } savedDeviceName)
        {
            selectedOutputDeviceDisplayName = savedDeviceName;
        }

        _appliedSelectedOutputDeviceDisplayName = selectedOutputDeviceDisplayName;

        var savedVolumeCurve = saved.VolumeCurve;
        useLogarithmicVolumeScale = savedVolumeCurve == VolumeCurve.Logarithmic;
        _playbackControls.VolumeCurve = savedVolumeCurve;
        _appliedUseLogarithmicVolumeScale = useLogarithmicVolumeScale;

        _playbackControls.Volume = saved.Volume ?? _playbackControls.Volume;

        seekStepSeconds = saved.SeekStepSeconds;
        _playbackControls.SeekStepSeconds = seekStepSeconds;
        _appliedSeekStepSeconds = seekStepSeconds;
    }

    partial void OnRestoreQueueOnStartupChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnSelectedOutputDeviceDisplayNameChanged(string value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnUseLogarithmicVolumeScaleChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnSeekStepSecondsChanged(int value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnUseWaveformSliderChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    public Task OutputDeviceSwitchTask { get; private set; } = Task.CompletedTask;

    [RelayCommand]
    private void ApplyPlaybackSettings()
    {
        var curve = UseLogarithmicVolumeScale ? VolumeCurve.Logarithmic : VolumeCurve.Linear;
        var deviceName = SelectedOutputDeviceDisplayName == Strings.SystemDefaultAudioDevice ? null : SelectedOutputDeviceDisplayName;
        _settings.Update(s => s with
        {
            RestoreQueueOnStartup = RestoreQueueOnStartup,
            UseWaveformSlider = UseWaveformSlider,
            VolumeCurve = curve,
            SeekStepSeconds = SeekStepSeconds,
            OutputDeviceName = deviceName,
        });

        _appliedRestoreQueueOnStartup = RestoreQueueOnStartup;

        _playbackControls.UseWaveformSlider = UseWaveformSlider;
        _appliedUseWaveformSlider = UseWaveformSlider;

        _playbackControls.VolumeCurve = curve;
        _appliedUseLogarithmicVolumeScale = UseLogarithmicVolumeScale;

        _playbackControls.SeekStepSeconds = SeekStepSeconds;
        _appliedSeekStepSeconds = SeekStepSeconds;

        OutputDeviceSwitchTask = _outputDeviceService.SetOutputDeviceAsync(deviceName);
        _appliedSelectedOutputDeviceDisplayName = SelectedOutputDeviceDisplayName;

        OnPropertyChanged(nameof(HasPendingChanges));
    }

    [RelayCommand]
    private void CancelPlaybackSettings()
    {
        RestoreQueueOnStartup = _appliedRestoreQueueOnStartup;
        SelectedOutputDeviceDisplayName = _appliedSelectedOutputDeviceDisplayName;
        UseLogarithmicVolumeScale = _appliedUseLogarithmicVolumeScale;
        SeekStepSeconds = _appliedSeekStepSeconds;
        UseWaveformSlider = _appliedUseWaveformSlider;
    }

    public async Task ApplyPersistedOutputDeviceAsync()
    {
        var devices = await _outputDeviceService.GetOutputDevicesAsync();

        OutputDeviceDisplayNames.Clear();
        OutputDeviceDisplayNames.Add(Strings.SystemDefaultAudioDevice);
        foreach (var device in devices)
        {
            OutputDeviceDisplayNames.Add(device.Name);
        }

        if (!OutputDeviceDisplayNames.Contains(SelectedOutputDeviceDisplayName))
        {
            SelectedOutputDeviceDisplayName = Strings.SystemDefaultAudioDevice;
            _appliedSelectedOutputDeviceDisplayName = Strings.SystemDefaultAudioDevice;
        }

        if (SelectedOutputDeviceDisplayName != Strings.SystemDefaultAudioDevice)
        {
            await _outputDeviceService.SetOutputDeviceAsync(SelectedOutputDeviceDisplayName);
        }
    }
}
