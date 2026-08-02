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

    // MVVMTK0034: backing-field writes are only allowed inside the constructor.
    private bool _suppressOutputDeviceChangeSideEffects;

    public ObservableCollection<string> OutputDeviceDisplayNames { get; } = [Strings.SystemDefaultAudioDevice];

    [ObservableProperty]
    private bool restoreQueueOnStartup;

    [ObservableProperty]
    private string selectedOutputDeviceDisplayName = Strings.SystemDefaultAudioDevice;

    [ObservableProperty]
    private bool useLogarithmicVolumeScale;

    public bool HasPendingChanges => false;

    public ICommand ApplyCommand { get; } = new RelayCommand(() => { });

    public ICommand CancelCommand { get; } = new RelayCommand(() => { });

    public PlaybackSettingsViewModel(string settingsFilePath, IOutputDeviceService outputDeviceService, PlaybackControlsViewModel playbackControls)
    {
        _settingsFilePath = settingsFilePath;
        _outputDeviceService = outputDeviceService;
        _playbackControls = playbackControls;
        restoreQueueOnStartup = SettingsStore.LoadRestoreQueueOnStartup(settingsFilePath);

        if (SettingsStore.LoadOutputDeviceName(settingsFilePath) is { } savedDeviceName)
        {
            selectedOutputDeviceDisplayName = savedDeviceName;
        }

        var savedVolumeCurve = SettingsStore.LoadVolumeCurve(settingsFilePath);
        useLogarithmicVolumeScale = savedVolumeCurve == VolumeCurve.Logarithmic;
        _playbackControls.VolumeCurve = savedVolumeCurve;
        _playbackControls.Volume = SettingsStore.LoadVolume(settingsFilePath) ?? _playbackControls.Volume;
    }

    partial void OnRestoreQueueOnStartupChanged(bool value) =>
        SettingsStore.SaveRestoreQueueOnStartup(_settingsFilePath, value);

    partial void OnUseLogarithmicVolumeScaleChanged(bool value)
    {
        var curve = value ? VolumeCurve.Logarithmic : VolumeCurve.Linear;
        SettingsStore.SaveVolumeCurve(_settingsFilePath, curve);
        _playbackControls.VolumeCurve = curve;
    }

    public Task OutputDeviceSwitchTask { get; private set; } = Task.CompletedTask;

    partial void OnSelectedOutputDeviceDisplayNameChanged(string value)
    {
        if (_suppressOutputDeviceChangeSideEffects)
            return;

        var deviceName = value == Strings.SystemDefaultAudioDevice ? null : value;
        SettingsStore.SaveOutputDeviceName(_settingsFilePath, deviceName);
        OutputDeviceSwitchTask = _outputDeviceService.SetOutputDeviceAsync(deviceName);
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
            _suppressOutputDeviceChangeSideEffects = true;
            SelectedOutputDeviceDisplayName = Strings.SystemDefaultAudioDevice;
            _suppressOutputDeviceChangeSideEffects = false;
        }

        if (SelectedOutputDeviceDisplayName != Strings.SystemDefaultAudioDevice)
        {
            await _outputDeviceService.SetOutputDeviceAsync(SelectedOutputDeviceDisplayName);
        }
    }
}
