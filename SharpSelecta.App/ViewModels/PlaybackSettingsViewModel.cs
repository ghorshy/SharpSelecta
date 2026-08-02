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
    private readonly IAudioEngine _audioEngine;
    private readonly PlaybackControlsViewModel _playbackControls;

    // Guards OnSelectedOutputDeviceDisplayNameChanged's persist/apply side effects while
    // ApplyPersistedOutputDeviceAsync resets the selection back to "System Default" internally
    // (the previously selected device isn't currently present) - that reset shouldn't overwrite
    // the saved preference. A direct backing-field write would be simpler, but the MVVM toolkit
    // analyzer (MVVMTK0034) only permits that inside the constructor.
    private bool _suppressOutputDeviceChangeSideEffects;

    // "System Default" is a synthetic entry, not a real device name - it's what SetOutputDevice(null)
    // means. Populated for real once the engine is ready, see ApplyPersistedOutputDevice.
    public ObservableCollection<string> OutputDeviceDisplayNames { get; } = [Strings.SystemDefaultAudioDevice];

    [ObservableProperty]
    private bool restoreQueueOnStartup;

    [ObservableProperty]
    private string selectedOutputDeviceDisplayName = Strings.SystemDefaultAudioDevice;

    [ObservableProperty]
    private bool useLogarithmicVolumeScale;

    // Toggle saves immediately, like the Library column-visibility checkboxes - there's nothing
    // expensive to batch behind Apply/Cancel here, unlike folder paths which trigger a rescan.
    public bool HasPendingChanges => false;

    public ICommand ApplyCommand { get; } = new RelayCommand(() => { });

    public ICommand CancelCommand { get; } = new RelayCommand(() => { });

    public PlaybackSettingsViewModel(string settingsFilePath, IAudioEngine audioEngine, PlaybackControlsViewModel playbackControls)
    {
        _settingsFilePath = settingsFilePath;
        _audioEngine = audioEngine;
        _playbackControls = playbackControls;
        restoreQueueOnStartup = SettingsStore.LoadRestoreQueueOnStartup(settingsFilePath);

        // Assigning the backing field directly (not the generated property): the saved device name
        // isn't in OutputDeviceDisplayNames yet (that only happens once the engine is ready, see
        // ApplyPersistedOutputDevice), so going through the setter here would just re-save the exact
        // value it was loaded from.
        if (SettingsStore.LoadOutputDeviceName(settingsFilePath) is { } savedDeviceName)
        {
            selectedOutputDeviceDisplayName = savedDeviceName;
        }

        var savedVolumeCurve = SettingsStore.LoadVolumeCurve(settingsFilePath);
        useLogarithmicVolumeScale = savedVolumeCurve == VolumeCurve.Logarithmic;
        // Goes through PlaybackControlsViewModel's own public setter (not a direct field write -
        // that guard only applies to this class's own [ObservableProperty] fields), which
        // immediately re-applies it to the (possibly still uninitialized) engine, same as the
        // cached-volume pattern OwnAudioEngine already uses for Volume itself.
        _playbackControls.VolumeCurve = savedVolumeCurve;
        // Volume must load after the curve is in place so the initial engine amplitude is computed
        // with the curve actually used this session - keeping both loads here makes that ordering
        // one class's internal concern instead of a cross-constructor invariant.
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

    // The in-flight switch from OnSelectedOutputDeviceDisplayNameChanged - fire-and-forget from
    // the UI's perspective (a property-changed handler can't await), kept awaitable so tests can
    // deterministically wait for the engine call, same as RefreshPositionAsync's rationale.
    public Task OutputDeviceSwitchTask { get; private set; } = Task.CompletedTask;

    partial void OnSelectedOutputDeviceDisplayNameChanged(string value)
    {
        if (_suppressOutputDeviceChangeSideEffects)
            return;

        var deviceName = value == Strings.SystemDefaultAudioDevice ? null : value;
        SettingsStore.SaveOutputDeviceName(_settingsFilePath, deviceName);
        // Off the UI thread: this fires from the Settings ComboBox, and SetOutputDevice is a
        // blocking ~1s native Stop/switch/Start cycle (see OwnAudioEngine).
        OutputDeviceSwitchTask = Task.Run(() => _audioEngine.SetOutputDevice(deviceName));
    }

    // Called once after the engine finishes initializing (App.axaml.cs) - IAudioEngine.GetOutputDevices
    // and SetOutputDevice both require that. Refreshes the device list and re-applies whatever device
    // was selected in a previous session. Both engine calls run off the calling (UI) thread -
    // GetOutputDevices is a native enumeration and SetOutputDevice a full Stop/switch/Start cycle
    // (~1s measured, see OwnAudioEngine) that was stalling first paint - while the UI-bound
    // ObservableCollection updates stay on it.
    public async Task ApplyPersistedOutputDeviceAsync()
    {
        var devices = await Task.Run(_audioEngine.GetOutputDevices);

        OutputDeviceDisplayNames.Clear();
        OutputDeviceDisplayNames.Add(Strings.SystemDefaultAudioDevice);
        foreach (var device in devices)
        {
            OutputDeviceDisplayNames.Add(device.Name);
        }

        if (!OutputDeviceDisplayNames.Contains(SelectedOutputDeviceDisplayName))
        {
            // The previously selected device isn't currently present (unplugged, renamed) - fall
            // back to displaying "System Default" without touching the saved preference, in case
            // the device reappears on a later launch.
            _suppressOutputDeviceChangeSideEffects = true;
            SelectedOutputDeviceDisplayName = Strings.SystemDefaultAudioDevice;
            _suppressOutputDeviceChangeSideEffects = false;
        }

        // The engine has just initialized on the system default, so "System Default" (whether
        // saved as such or fallen back to above) needs no switch at all - re-applying it was a
        // redundant ~1s Stop/switch/Start of the device the engine is already on. Only an
        // explicitly saved, currently present device actually moves the engine off the default.
        if (SelectedOutputDeviceDisplayName != Strings.SystemDefaultAudioDevice)
        {
            var deviceName = SelectedOutputDeviceDisplayName;
            await Task.Run(() => _audioEngine.SetOutputDevice(deviceName));
        }
    }
}
