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
    private readonly string _settingsFilePath;
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

    public PlaybackSettingsViewModel(string settingsFilePath, IOutputDeviceService outputDeviceService, PlaybackControlsViewModel playbackControls)
    {
        _settingsFilePath = settingsFilePath;
        _outputDeviceService = outputDeviceService;
        _playbackControls = playbackControls;

        RestoreQueueOnStartup = SettingsStore.LoadRestoreQueueOnStartup(settingsFilePath);
        _appliedRestoreQueueOnStartup = RestoreQueueOnStartup;

        useWaveformSlider = SettingsStore.LoadUseWaveformSlider(settingsFilePath);
        _playbackControls.UseWaveformSlider = useWaveformSlider;
        _appliedUseWaveformSlider = useWaveformSlider;

        if (SettingsStore.LoadOutputDeviceName(settingsFilePath) is { } savedDeviceName)
        {
            selectedOutputDeviceDisplayName = savedDeviceName;
        }

        _appliedSelectedOutputDeviceDisplayName = selectedOutputDeviceDisplayName;

        var savedVolumeCurve = SettingsStore.LoadVolumeCurve(settingsFilePath);
        useLogarithmicVolumeScale = savedVolumeCurve == VolumeCurve.Logarithmic;
        _playbackControls.VolumeCurve = savedVolumeCurve;
        _appliedUseLogarithmicVolumeScale = useLogarithmicVolumeScale;

        _playbackControls.Volume = SettingsStore.LoadVolume(settingsFilePath) ?? _playbackControls.Volume;

        seekStepSeconds = SettingsStore.LoadSeekStepSeconds(settingsFilePath);
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
        SettingsStore.SaveRestoreQueueOnStartup(_settingsFilePath, RestoreQueueOnStartup);
        _appliedRestoreQueueOnStartup = RestoreQueueOnStartup;

        SettingsStore.SaveUseWaveformSlider(_settingsFilePath, UseWaveformSlider);
        _playbackControls.UseWaveformSlider = UseWaveformSlider;
        _appliedUseWaveformSlider = UseWaveformSlider;

        var curve = UseLogarithmicVolumeScale ? VolumeCurve.Logarithmic : VolumeCurve.Linear;
        SettingsStore.SaveVolumeCurve(_settingsFilePath, curve);
        _playbackControls.VolumeCurve = curve;
        _appliedUseLogarithmicVolumeScale = UseLogarithmicVolumeScale;

        SettingsStore.SaveSeekStepSeconds(_settingsFilePath, SeekStepSeconds);
        _playbackControls.SeekStepSeconds = SeekStepSeconds;
        _appliedSeekStepSeconds = SeekStepSeconds;

        var deviceName = SelectedOutputDeviceDisplayName == Strings.SystemDefaultAudioDevice ? null : SelectedOutputDeviceDisplayName;
        SettingsStore.SaveOutputDeviceName(_settingsFilePath, deviceName);
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
