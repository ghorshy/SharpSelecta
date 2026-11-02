using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// Preset is null for Custom (hand-edited bands).
public sealed record EqualizerPresetOption(EqualizerPreset? Preset, string Label)
{
    public const string CustomSettingsKey = "Custom";

    public string SettingsKey => Preset?.ToString() ?? CustomSettingsKey;
}

public partial class EqualizerViewModel : ObservableObject
{
    private readonly SettingsStore _settings;
    private readonly IAudioEngine _audioEngine;

    // MVVMTK0034: backing-field writes are only allowed inside the constructor.
    private bool _suppressPresetChangeSideEffects;
    private bool _suppressBandGainSideEffects;

    public ObservableCollection<EqualizerBandViewModel> Bands { get; }

    public IReadOnlyList<EqualizerPresetOption> PresetOptions { get; } =
    [
        new(EqualizerPreset.Default, Strings.EqualizerPresetDefault),
        new(EqualizerPreset.Bass, Strings.EqualizerPresetBass),
        new(EqualizerPreset.Treble, Strings.EqualizerPresetTreble),
        new(EqualizerPreset.Rock, Strings.EqualizerPresetRock),
        new(EqualizerPreset.Classical, Strings.EqualizerPresetClassical),
        new(EqualizerPreset.Pop, Strings.EqualizerPresetPop),
        new(EqualizerPreset.Jazz, Strings.EqualizerPresetJazz),
        new(EqualizerPreset.Voice, Strings.EqualizerPresetVoice),
        new(null, Strings.EqualizerPresetCustom),
    ];

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    private EqualizerPresetOption DefaultOption => PresetOptions[0];
    private EqualizerPresetOption CustomOption => PresetOptions[^1];

    [ObservableProperty]
    public partial EqualizerPresetOption SelectedPreset { get; set; }

    public EqualizerViewModel(SettingsStore settings, IAudioEngine audioEngine)
    {
        _settings = settings;
        _audioEngine = audioEngine;
        _suppressPresetChangeSideEffects = true;
        SelectedPreset = DefaultOption;
        _suppressPresetChangeSideEffects = false;

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
        _settings.Update(s => s with { EqualizerEnabled = value });
    }

    partial void OnSelectedPresetChanged(EqualizerPresetOption value)
    {
        if (_suppressPresetChangeSideEffects || value.Preset is not { } preset)
            return;

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
        SelectedPreset = CustomOption;
        _suppressPresetChangeSideEffects = false;
    }

    public void PersistBandGains() => _settings.Update(s => s with
    {
        EqualizerBandGainsDb = Bands.Select(b => (float)b.GainDb).ToList(),
        EqualizerPresetName = SelectedPreset.SettingsKey,
    });

    public void ApplyPersistedState()
    {
        var savedGains = _settings.Current.EqualizerBandGainsDb;
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

        var savedPresetName = _settings.Current.EqualizerPresetName;
        _suppressPresetChangeSideEffects = true;
        SelectedPreset = PresetOptions.FirstOrDefault(o => o.SettingsKey == savedPresetName) ?? DefaultOption;
        _suppressPresetChangeSideEffects = false;

        var savedEnabled = _settings.Current.EqualizerEnabled;
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
