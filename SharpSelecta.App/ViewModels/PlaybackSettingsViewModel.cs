using System.Collections.ObjectModel;
using System.Linq;
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

    // Guards OnSelectedOutputDeviceDisplayNameChanged's persist/apply side effects while
    // ApplyPersistedOutputDevice resets the selection back to "System Default" internally (the
    // previously selected device isn't currently present) - that reset shouldn't itself overwrite
    // the saved preference, in case the device reappears later.
    private bool _suppressOutputDeviceChangeSideEffects;

    // "System Default" is a synthetic entry, not a real device name - it's what SetOutputDevice(null)
    // means. Populated for real once the engine is ready, see ApplyPersistedOutputDevice.
    public ObservableCollection<string> OutputDeviceDisplayNames { get; } = [Strings.SystemDefaultAudioDevice];

    [ObservableProperty]
    private bool restoreQueueOnStartup;

    [ObservableProperty]
    private string selectedOutputDeviceDisplayName = Strings.SystemDefaultAudioDevice;

    // Toggle saves immediately, like the Library column-visibility checkboxes - there's nothing
    // expensive to batch behind Apply/Cancel here, unlike folder paths which trigger a rescan.
    public bool HasPendingChanges => false;

    public ICommand ApplyCommand { get; } = new RelayCommand(() => { });

    public ICommand CancelCommand { get; } = new RelayCommand(() => { });

    public PlaybackSettingsViewModel(string settingsFilePath, IAudioEngine audioEngine)
    {
        _settingsFilePath = settingsFilePath;
        _audioEngine = audioEngine;
        restoreQueueOnStartup = LibrarySettingsStore.LoadRestoreQueueOnStartup(settingsFilePath);

        // Assigning the backing field directly (not the generated property): the saved device name
        // isn't in OutputDeviceDisplayNames yet (that only happens once the engine is ready, see
        // ApplyPersistedOutputDevice), so going through the setter here would just re-save the exact
        // value it was loaded from.
        if (LibrarySettingsStore.LoadOutputDeviceName(settingsFilePath) is { } savedDeviceName)
        {
            selectedOutputDeviceDisplayName = savedDeviceName;
        }
    }

    partial void OnRestoreQueueOnStartupChanged(bool value) =>
        LibrarySettingsStore.SaveRestoreQueueOnStartup(_settingsFilePath, value);

    partial void OnSelectedOutputDeviceDisplayNameChanged(string value)
    {
        if (_suppressOutputDeviceChangeSideEffects)
            return;

        var deviceName = value == Strings.SystemDefaultAudioDevice ? null : value;
        LibrarySettingsStore.SaveOutputDeviceName(_settingsFilePath, deviceName);
        _audioEngine.SetOutputDevice(deviceName);
    }

    // Called once after the engine finishes initializing (App.axaml.cs) - IAudioEngine.GetOutputDevices
    // and SetOutputDevice both require that. Refreshes the device list and re-applies whatever device
    // was selected in a previous session.
    public void ApplyPersistedOutputDevice()
    {
        OutputDeviceDisplayNames.Clear();
        OutputDeviceDisplayNames.Add(Strings.SystemDefaultAudioDevice);
        foreach (var device in _audioEngine.GetOutputDevices())
        {
            OutputDeviceDisplayNames.Add(device.Name);
        }

        if (!OutputDeviceDisplayNames.Contains(SelectedOutputDeviceDisplayName))
        {
            // The previously selected device isn't currently present (unplugged, renamed) - fall
            // back to displaying "System Default" without touching the saved preference or calling
            // SetOutputDevice a second time (that happens explicitly below regardless).
            _suppressOutputDeviceChangeSideEffects = true;
            SelectedOutputDeviceDisplayName = Strings.SystemDefaultAudioDevice;
            _suppressOutputDeviceChangeSideEffects = false;
        }

        var deviceNameToApply = SelectedOutputDeviceDisplayName == Strings.SystemDefaultAudioDevice ? null : SelectedOutputDeviceDisplayName;
        _audioEngine.SetOutputDevice(deviceNameToApply);
    }
}
