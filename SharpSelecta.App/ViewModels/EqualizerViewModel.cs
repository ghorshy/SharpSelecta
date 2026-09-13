using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class EqualizerViewModel : ObservableObject
{
    public const string CustomPresetName = "Custom";

    private readonly string _settingsFilePath;
    private readonly IAudioEngine _audioEngine;

    // MVVMTK0034: backing-field writes are only allowed inside the constructor.
    private bool _suppressPresetChangeSideEffects;
    private bool _suppressBandGainSideEffects;

    public ObservableCollection<EqualizerBandViewModel> Bands { get; }

    public IReadOnlyList<string> PresetNames { get; } = [.. Enum.GetNames<EqualizerPreset>(), CustomPresetName];

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    [ObservableProperty]
    public partial string SelectedPresetName { get; set; } = nameof(EqualizerPreset.Default);

    public EqualizerViewModel(string settingsFilePath, IAudioEngine audioEngine)
    {
        _settingsFilePath = settingsFilePath;
        _audioEngine = audioEngine;

        var frequencies = audioEngine.EqualizerBandFrequenciesHz;
        var gains = audioEngine.EqualizerBandGainsDb;
        Bands =
        [
            .. frequencies.Select((hz, i) => new EqualizerBandViewModel(i, FormatFrequency(hz), gains[i]))
        ];

        foreach (var band in Bands)
        {
            band.PropertyChanged += OnBandGainChanged;
        }
    }

    private static string FormatFrequency(int hz) => hz >= 1000 ? $"{hz / 1000} kHz" : $"{hz} Hz";

    partial void OnEnabledChanged(bool value)
    {
        _audioEngine.EqualizerEnabled = value;
        SettingsStore.SaveEqualizerEnabled(_settingsFilePath, value);
    }

    partial void OnSelectedPresetNameChanged(string value)
    {
        if (_suppressPresetChangeSideEffects || value == CustomPresetName)
            return;

        var preset = Enum.Parse<EqualizerPreset>(value);
        _audioEngine.ApplyEqualizerPreset(preset);

        _suppressBandGainSideEffects = true;
        try
        {
            var gains = _audioEngine.EqualizerBandGainsDb;
            for (var i = 0; i < Bands.Count; i++)
            {
                Bands[i].GainDb = gains[i];
            }
        }
        finally
        {
            _suppressBandGainSideEffects = false;
        }

        PersistBandGains();
    }

    private void OnBandGainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressBandGainSideEffects || e.PropertyName != nameof(EqualizerBandViewModel.GainDb) || sender is not EqualizerBandViewModel band)
            return;

        _audioEngine.SetEqualizerBandGain(band.Index, (float)band.GainDb);

        _suppressPresetChangeSideEffects = true;
        SelectedPresetName = CustomPresetName;
        _suppressPresetChangeSideEffects = false;
    }

    public void PersistBandGains()
    {
        SettingsStore.SaveEqualizerBandGainsDb(_settingsFilePath, Bands.Select(b => (float)b.GainDb).ToList());
        SettingsStore.SaveEqualizerPresetName(_settingsFilePath, SelectedPresetName);
    }

    public void ApplyPersistedState()
    {
        var savedGains = SettingsStore.LoadEqualizerBandGainsDb(_settingsFilePath);
        if (savedGains is { Count: 10 })
        {
            _suppressBandGainSideEffects = true;
            try
            {
                for (var i = 0; i < Bands.Count; i++)
                {
                    Bands[i].GainDb = savedGains[i];
                    _audioEngine.SetEqualizerBandGain(i, savedGains[i]);
                }
            }
            finally
            {
                _suppressBandGainSideEffects = false;
            }
        }

        var savedPresetName = SettingsStore.LoadEqualizerPresetName(_settingsFilePath);
        _suppressPresetChangeSideEffects = true;
        SelectedPresetName = savedPresetName is not null && PresetNames.Contains(savedPresetName)
            ? savedPresetName
            : nameof(EqualizerPreset.Default);
        _suppressPresetChangeSideEffects = false;

        var savedEnabled = SettingsStore.LoadEqualizerEnabled(_settingsFilePath);
        var previouslyEnabled = Enabled;
        Enabled = savedEnabled;
        if (previouslyEnabled == savedEnabled)
        {
            // Enabled's setter dedups when the value already matched (e.g. toggled pre-init);
            // push explicitly so a value the engine missed earlier still reaches it now.
            _audioEngine.EqualizerEnabled = savedEnabled;
        }
    }
}
