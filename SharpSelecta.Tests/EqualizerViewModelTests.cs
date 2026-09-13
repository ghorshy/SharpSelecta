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
        return new EqualizerViewModel(settingsFilePath ?? CreateTempSettingsPath(), audioEngine);
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

    [Test]
    public async Task EnabledChanged_PushesToEngineAndPersists()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateViewModel(out var audioEngine, settingsPath);

            vm.Enabled = true;

            audioEngine.Received(1).EqualizerEnabled = true;
            await Assert.That(SettingsStore.LoadEqualizerEnabled(settingsPath)).IsTrue();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SelectedPresetNameChanged_AppliesPresetAndRefreshesBands()
    {
        var vm = CreateViewModel(out var audioEngine);
        audioEngine.EqualizerBandGainsDb.Returns([3f, 3f, 0f, 0f, 0f, 0f, 0f, 0f, -2f, -2f]);

        vm.SelectedPresetName = nameof(EqualizerPreset.Bass);

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
        await Assert.That(vm.SelectedPresetName).IsEqualTo(EqualizerViewModel.CustomPresetName);
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

            var saved = SettingsStore.LoadEqualizerBandGainsDb(settingsPath);
            await Assert.That(saved![0]).IsEqualTo(5f);
            await Assert.That(SettingsStore.LoadEqualizerPresetName(settingsPath)).IsEqualTo(EqualizerViewModel.CustomPresetName);
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
            SettingsStore.SaveEqualizerEnabled(settingsPath, true);
            SettingsStore.SaveEqualizerPresetName(settingsPath, EqualizerViewModel.CustomPresetName);
            SettingsStore.SaveEqualizerBandGainsDb(settingsPath, [1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f]);

            var vm = CreateViewModel(out var audioEngine, settingsPath);

            vm.ApplyPersistedState();

            await Assert.That(vm.Enabled).IsTrue();
            await Assert.That(vm.SelectedPresetName).IsEqualTo(EqualizerViewModel.CustomPresetName);
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
            SettingsStore.SaveEqualizerEnabled(settingsPath, false);
            SettingsStore.SaveEqualizerPresetName(settingsPath, nameof(EqualizerPreset.Bass));
            SettingsStore.SaveEqualizerBandGainsDb(settingsPath, [1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f]);

            var vm = CreateViewModel(out var audioEngine, settingsPath);
            // Stub the engine's Bass-preset gains to diverge from the saved gains, so a
            // spurious re-apply of the preset would be detectable via the restored values.
            audioEngine.EqualizerBandGainsDb.Returns([-1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f]);

            vm.ApplyPersistedState();

            await Assert.That(vm.SelectedPresetName).IsEqualTo(nameof(EqualizerPreset.Bass));
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
