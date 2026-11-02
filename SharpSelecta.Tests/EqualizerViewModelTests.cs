using NSubstitute;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class EqualizerViewModelTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-equalizer-vm-tests-{Guid.NewGuid():N}.json");

    private static readonly int[] StandardFrequencies = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    private static EqualizerViewModel CreateViewModel(out IAudioEngine audioEngine, string? settingsFilePath = null)
    {
        audioEngine = Substitute.For<IAudioEngine>();
        audioEngine.EqualizerBandFrequenciesHz.Returns(StandardFrequencies);
        audioEngine.EqualizerBandGainsDb.Returns(new float[10]);
        return new EqualizerViewModel(new SettingsStore(settingsFilePath ?? CreateTempSettingsPath()), audioEngine);
    }

    [Test]
    public async Task Constructor_BuildsTenBandsWithFrequencyLabels()
    {
        var vm = CreateViewModel(out _);

        await Assert.That(vm.Bands.Count).IsEqualTo(10);
        await Assert.That(vm.Bands[0].Label).IsEqualTo("31 Hz");
        await Assert.That(vm.Bands[5].Label).IsEqualTo("1 kHz");
        await Assert.That(vm.Bands[9].Index).IsEqualTo(9);
    }

    private static EqualizerPresetOption Option(EqualizerViewModel vm, EqualizerPreset? preset) =>
        vm.PresetOptions.Single(o => o.Preset == preset);

    [Test]
    public async Task PresetOptions_ListEveryPresetWithLocalizedLabelsThenCustom()
    {
        var vm = CreateViewModel(out _);

        await Assert.That(string.Join(",", vm.PresetOptions.Select(o => o.Label)))
            .IsEqualTo("Default,Bass,Treble,Rock,Classical,Pop,Jazz,Voice,Custom");
        await Assert.That(vm.PresetOptions[^1].Preset).IsNull();
    }

    [Test]
    public async Task EnabledChanged_PushesToEngineAndPersists()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateViewModel(out var audioEngine, settingsPath);

            vm.Enabled = true;

            audioEngine.Received(1).EqualizerEnabled = true;
            await Assert.That(new SettingsStore(settingsPath).Current.EqualizerEnabled).IsTrue();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SelectedPresetChanged_AppliesPresetAndRefreshesBands()
    {
        var vm = CreateViewModel(out var audioEngine);
        audioEngine.EqualizerBandGainsDb.Returns([3f, 3f, 0f, 0f, 0f, 0f, 0f, 0f, -2f, -2f]);

        vm.SelectedPreset = Option(vm, EqualizerPreset.Bass);

        audioEngine.Received(1).ApplyEqualizerPreset(EqualizerPreset.Bass);
        await Assert.That(vm.Bands[0].GainDb).IsEqualTo(3.0);
        await Assert.That(vm.Bands[8].GainDb).IsEqualTo(-2.0);
    }

    [Test]
    public async Task EditingABandGain_PushesToEngineAndSwitchesPresetToCustom()
    {
        var vm = CreateViewModel(out var audioEngine);

        vm.Bands[2].GainDb = 4.5;

        audioEngine.Received(1).SetEqualizerBandGain(2, 4.5f);
        await Assert.That(vm.SelectedPreset.Preset).IsNull();
    }

    [Test]
    public async Task PersistBandGains_SavesCurrentGainsAndPresetName()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateViewModel(out _, settingsPath);
            vm.Bands[0].GainDb = 5;

            vm.PersistBandGains();

            var saved = new SettingsStore(settingsPath).Current.EqualizerBandGainsDb;
            await Assert.That(saved![0]).IsEqualTo(5f);
            await Assert.That(new SettingsStore(settingsPath).Current.EqualizerPresetName).IsEqualTo("Custom");
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task ApplyPersistedState_RestoresGainsPresetAndEnabled()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            new SettingsStore(settingsPath).Update(s => s with { EqualizerEnabled = true });
            new SettingsStore(settingsPath).Update(s => s with { EqualizerPresetName = "Custom" });
            new SettingsStore(settingsPath).Update(s => s with { EqualizerBandGainsDb = [1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f] });

            var vm = CreateViewModel(out var audioEngine, settingsPath);

            vm.ApplyPersistedState();

            await Assert.That(vm.Enabled).IsTrue();
            await Assert.That(vm.SelectedPreset.Preset).IsNull();
            await Assert.That(vm.Bands[9].GainDb).IsEqualTo(10.0);
            audioEngine.Received(1).SetEqualizerBandGain(9, 10f);
            audioEngine.Received(1).EqualizerEnabled = true;
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task ApplyPersistedState_RestoresNamedPresetWithDivergingGainsWithoutReapplyingPreset()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            new SettingsStore(settingsPath).Update(s => s with { EqualizerEnabled = false });
            new SettingsStore(settingsPath).Update(s => s with { EqualizerPresetName = nameof(EqualizerPreset.Bass) });
            new SettingsStore(settingsPath).Update(s => s with { EqualizerBandGainsDb = [1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f] });

            var vm = CreateViewModel(out var audioEngine, settingsPath);
            // Stub the engine's Bass-preset gains to diverge from the saved gains, so a
            // spurious re-apply of the preset would be detectable via the restored values.
            audioEngine.EqualizerBandGainsDb.Returns([-1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f]);

            vm.ApplyPersistedState();

            await Assert.That(vm.SelectedPreset.Preset).IsEqualTo(EqualizerPreset.Bass);
            await Assert.That(vm.Bands[0].GainDb).IsEqualTo(1.0);
            await Assert.That(vm.Bands[9].GainDb).IsEqualTo(10.0);
            audioEngine.DidNotReceive().ApplyEqualizerPreset(Arg.Any<EqualizerPreset>());
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }
}
