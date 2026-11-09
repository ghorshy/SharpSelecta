using System.Collections.ObjectModel;
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
    private bool _appliedResumePlaybackOnLaunch;
    private OpenedFilesAction _appliedOpenedFilesAction;
    private TrackStartAction _appliedTrackStartAction;
    private VolumeNormalization _appliedVolumeNormalization;
    private double _appliedUntaggedGainDb;
    private string? _appliedOutputDeviceName;
    private bool _appliedUseLogarithmicVolumeScale;
    private int _appliedSeekStepSeconds;
    private int _appliedPreviousRestartThresholdSeconds;
    private bool _appliedPauseOnDeviceDisconnect;
    private bool _appliedUseWaveformSlider;

    public ObservableCollection<OutputDeviceOption> OutputDevices { get; } = [OutputDeviceOption.SystemDefault];

    [ObservableProperty]
    public partial bool RestoreQueueOnStartup { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial bool ResumePlaybackOnLaunch { get; set; }

    public IReadOnlyList<OpenedFilesActionOption> OpenedFilesActionOptions { get; } =
    [
        new(OpenedFilesAction.ReplaceQueueAndPlay, Strings.OpenedFilesReplaceQueueAndPlay),
        new(OpenedFilesAction.AddToQueue, Strings.OpenedFilesAddToQueue),
        new(OpenedFilesAction.PlayNext, Strings.OpenedFilesPlayNext),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial OpenedFilesActionOption SelectedOpenedFilesActionOption { get; set; }

    public IReadOnlyList<TrackStartActionOption> TrackStartActionOptions { get; } =
    [
        new(TrackStartAction.InsertAndPlay, Strings.TrackStartInsertAndPlay),
        new(TrackStartAction.PlayFromHere, Strings.TrackStartPlayFromHere),
        new(TrackStartAction.ReplaceQueue, Strings.TrackStartReplaceQueue),
        new(TrackStartAction.AddToQueue, Strings.TrackStartAddToQueue),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial TrackStartActionOption SelectedTrackStartActionOption { get; set; }

    public IReadOnlyList<VolumeNormalizationOption> VolumeNormalizationOptions { get; } =
    [
        new(VolumeNormalization.Off, Strings.VolumeNormalizationOff),
        new(VolumeNormalization.Track, Strings.VolumeNormalizationTrack),
        new(VolumeNormalization.Album, Strings.VolumeNormalizationAlbum),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    [NotifyPropertyChangedFor(nameof(IsUntaggedGainEditable))]
    public partial VolumeNormalizationOption SelectedVolumeNormalizationOption { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    [NotifyPropertyChangedFor(nameof(UntaggedGainText))]
    public partial double UntaggedGainDb { get; set; }

    public bool IsUntaggedGainEditable => SelectedVolumeNormalizationOption.Normalization != VolumeNormalization.Off;

    public string UntaggedGainText => Strings.UntaggedGain(UntaggedGainDb);

    [ObservableProperty]
    private OutputDeviceOption selectedOutputDevice = OutputDeviceOption.SystemDefault;

    [ObservableProperty]
    private bool useLogarithmicVolumeScale;

    [ObservableProperty]
    private int seekStepSeconds = 5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial int PreviousRestartThresholdSeconds { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial bool PauseOnDeviceDisconnect { get; set; }

    [ObservableProperty]
    private bool useWaveformSlider;

    public string DisplayName => Strings.SettingsCategoryPlayback;

    // ?. because the bound ComboBox briefly pushes null while OutputDevices is repopulated.
    public bool HasPendingChanges =>
        RestoreQueueOnStartup != _appliedRestoreQueueOnStartup ||
        ResumePlaybackOnLaunch != _appliedResumePlaybackOnLaunch ||
        SelectedOpenedFilesActionOption.Action != _appliedOpenedFilesAction ||
        SelectedTrackStartActionOption.Action != _appliedTrackStartAction ||
        SelectedVolumeNormalizationOption.Normalization != _appliedVolumeNormalization ||
        UntaggedGainDb != _appliedUntaggedGainDb ||
        SelectedOutputDevice?.DeviceName != _appliedOutputDeviceName ||
        UseLogarithmicVolumeScale != _appliedUseLogarithmicVolumeScale ||
        SeekStepSeconds != _appliedSeekStepSeconds ||
        PreviousRestartThresholdSeconds != _appliedPreviousRestartThresholdSeconds ||
        PauseOnDeviceDisconnect != _appliedPauseOnDeviceDisconnect ||
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
        ResumePlaybackOnLaunch = saved.ResumePlaybackOnLaunch;
        _appliedResumePlaybackOnLaunch = ResumePlaybackOnLaunch;

        SelectedOpenedFilesActionOption = OptionFor(saved.OpenedFilesAction);
        _appliedOpenedFilesAction = saved.OpenedFilesAction;

        SelectedTrackStartActionOption = OptionFor(saved.TrackStartAction);
        _appliedTrackStartAction = saved.TrackStartAction;

        SelectedVolumeNormalizationOption = OptionFor(saved.VolumeNormalization);
        _appliedVolumeNormalization = saved.VolumeNormalization;
        UntaggedGainDb = saved.UntaggedGainDb;
        _appliedUntaggedGainDb = saved.UntaggedGainDb;

        useWaveformSlider = saved.UseWaveformSlider;
        _appliedUseWaveformSlider = useWaveformSlider;

        selectedOutputDevice = OutputDeviceOption.For(saved.OutputDeviceName);
        _appliedOutputDeviceName = saved.OutputDeviceName;

        useLogarithmicVolumeScale = saved.VolumeCurve == VolumeCurve.Logarithmic;
        _appliedUseLogarithmicVolumeScale = useLogarithmicVolumeScale;

        seekStepSeconds = saved.SeekStepSeconds;
        _playbackControls.SeekStepSeconds = seekStepSeconds;
        _appliedSeekStepSeconds = seekStepSeconds;

        PreviousRestartThresholdSeconds = saved.PreviousRestartThresholdSeconds;
        _playbackControls.PreviousRestartThresholdSeconds = saved.PreviousRestartThresholdSeconds;
        _appliedPreviousRestartThresholdSeconds = saved.PreviousRestartThresholdSeconds;

        PauseOnDeviceDisconnect = saved.PauseOnDeviceDisconnect;
        _playbackControls.PauseOnDeviceDisconnect = saved.PauseOnDeviceDisconnect;
        _appliedPauseOnDeviceDisconnect = saved.PauseOnDeviceDisconnect;
    }

    partial void OnRestoreQueueOnStartupChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnSelectedOutputDeviceChanged(OutputDeviceOption value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnUseLogarithmicVolumeScaleChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnSeekStepSecondsChanged(int value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnUseWaveformSliderChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    public Task OutputDeviceSwitchTask { get; private set; } = Task.CompletedTask;

    [RelayCommand]
    private void ApplyPlaybackSettings()
    {
        var curve = UseLogarithmicVolumeScale ? VolumeCurve.Logarithmic : VolumeCurve.Linear;
        var deviceName = SelectedOutputDevice.DeviceName;
        _settings.Update(s => s with
        {
            RestoreQueueOnStartup = RestoreQueueOnStartup,
            ResumePlaybackOnLaunch = ResumePlaybackOnLaunch,
            OpenedFilesAction = SelectedOpenedFilesActionOption.Action,
            TrackStartAction = SelectedTrackStartActionOption.Action,
            VolumeNormalization = SelectedVolumeNormalizationOption.Normalization,
            UntaggedGainDb = UntaggedGainDb,
            UseWaveformSlider = UseWaveformSlider,
            VolumeCurve = curve,
            SeekStepSeconds = SeekStepSeconds,
            PreviousRestartThresholdSeconds = PreviousRestartThresholdSeconds,
            PauseOnDeviceDisconnect = PauseOnDeviceDisconnect,
            OutputDeviceName = deviceName,
        });

        _appliedRestoreQueueOnStartup = RestoreQueueOnStartup;
        _appliedResumePlaybackOnLaunch = ResumePlaybackOnLaunch;
        _appliedOpenedFilesAction = SelectedOpenedFilesActionOption.Action;
        _appliedTrackStartAction = SelectedTrackStartActionOption.Action;

        _playbackControls.Normalization = SelectedVolumeNormalizationOption.Normalization;
        _appliedVolumeNormalization = SelectedVolumeNormalizationOption.Normalization;
        _playbackControls.UntaggedGainDb = UntaggedGainDb;
        _appliedUntaggedGainDb = UntaggedGainDb;

        _playbackControls.Waveform.UseSlider = UseWaveformSlider;
        _appliedUseWaveformSlider = UseWaveformSlider;

        _playbackControls.Volume.Curve = curve;
        _appliedUseLogarithmicVolumeScale = UseLogarithmicVolumeScale;

        _playbackControls.SeekStepSeconds = SeekStepSeconds;
        _appliedSeekStepSeconds = SeekStepSeconds;

        _playbackControls.PreviousRestartThresholdSeconds = PreviousRestartThresholdSeconds;
        _appliedPreviousRestartThresholdSeconds = PreviousRestartThresholdSeconds;

        _playbackControls.PauseOnDeviceDisconnect = PauseOnDeviceDisconnect;
        _appliedPauseOnDeviceDisconnect = PauseOnDeviceDisconnect;

        OutputDeviceSwitchTask = _outputDeviceService.SetOutputDeviceAsync(deviceName);
        _appliedOutputDeviceName = deviceName;

        OnPropertyChanged(nameof(HasPendingChanges));
    }

    [RelayCommand]
    private void CancelPlaybackSettings()
    {
        RestoreQueueOnStartup = _appliedRestoreQueueOnStartup;
        ResumePlaybackOnLaunch = _appliedResumePlaybackOnLaunch;
        SelectedOpenedFilesActionOption = OptionFor(_appliedOpenedFilesAction);
        SelectedTrackStartActionOption = OptionFor(_appliedTrackStartAction);
        SelectedVolumeNormalizationOption = OptionFor(_appliedVolumeNormalization);
        UntaggedGainDb = _appliedUntaggedGainDb;
        SelectedOutputDevice = OutputDeviceOption.For(_appliedOutputDeviceName);
        UseLogarithmicVolumeScale = _appliedUseLogarithmicVolumeScale;
        SeekStepSeconds = _appliedSeekStepSeconds;
        PreviousRestartThresholdSeconds = _appliedPreviousRestartThresholdSeconds;
        PauseOnDeviceDisconnect = _appliedPauseOnDeviceDisconnect;
        UseWaveformSlider = _appliedUseWaveformSlider;
    }

    private OpenedFilesActionOption OptionFor(OpenedFilesAction action) =>
        OpenedFilesActionOptions.FirstOrDefault(o => o.Action == action) ?? OpenedFilesActionOptions[0];

    private TrackStartActionOption OptionFor(TrackStartAction action) =>
        TrackStartActionOptions.FirstOrDefault(o => o.Action == action) ?? TrackStartActionOptions[0];

    private VolumeNormalizationOption OptionFor(VolumeNormalization normalization) =>
        VolumeNormalizationOptions.FirstOrDefault(o => o.Normalization == normalization) ?? VolumeNormalizationOptions[0];

    public async Task ApplyPersistedOutputDeviceAsync()
    {
        var devices = await _outputDeviceService.GetOutputDevicesAsync();

        // Read before Clear(): a bound ComboBox drops its selection when its items are cleared.
        var wanted = SelectedOutputDevice;
        OutputDevices.Clear();
        OutputDevices.Add(OutputDeviceOption.SystemDefault);
        foreach (var device in devices)
        {
            OutputDevices.Add(OutputDeviceOption.For(device.Name));
        }

        if (OutputDevices.Contains(wanted))
        {
            SelectedOutputDevice = wanted;
        }
        else
        {
            // Applied first, so the HasPendingChanges notification from the selection sees no pending change.
            _appliedOutputDeviceName = null;
            SelectedOutputDevice = OutputDeviceOption.SystemDefault;
        }

        if (SelectedOutputDevice.DeviceName is { } deviceName)
        {
            await _outputDeviceService.SetOutputDeviceAsync(deviceName);
        }
    }
}

public sealed record OpenedFilesActionOption(OpenedFilesAction Action, string DisplayName);

public sealed record TrackStartActionOption(TrackStartAction Action, string DisplayName);

public sealed record VolumeNormalizationOption(VolumeNormalization Normalization, string DisplayName);

// A null DeviceName is the system default, so no real device's name can be mistaken for it.
public sealed record OutputDeviceOption(string? DeviceName, string DisplayName)
{
    public static OutputDeviceOption SystemDefault => new(null, Strings.SystemDefaultAudioDevice);

    public static OutputDeviceOption For(string? deviceName) => deviceName is null ? SystemDefault : new(deviceName, deviceName);
}
