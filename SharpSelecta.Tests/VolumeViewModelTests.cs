using NSubstitute;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class VolumeViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Test]
    public async Task StartsFromTheSavedLevelAndCurve_AndAppliesThemToTheEngine()
    {
        var settingsPath = _temp.NewSettingsPath();
        new SettingsStore(settingsPath).Update(s => s with { Volume = 0.5, VolumeCurve = VolumeCurve.Logarithmic });
        var audioEngine = Substitute.For<IAudioEngine>();

        var vm = new VolumeViewModel(audioEngine, new SettingsStore(settingsPath));

        await Assert.That(vm.Level).IsEqualTo(0.5);
        await Assert.That(vm.Curve).IsEqualTo(VolumeCurve.Logarithmic);
        await Assert.That(audioEngine.Volume).IsEqualTo(0.25f);
    }

    [Test]
    public async Task Persist_SavesTheLevelForTheNextInstance()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = new VolumeViewModel(Substitute.For<IAudioEngine>(), new SettingsStore(settingsPath));
        vm.Level = 0.3;

        vm.Persist();

        var restarted = new VolumeViewModel(Substitute.For<IAudioEngine>(), new SettingsStore(settingsPath));
        await Assert.That(restarted.Level).IsEqualTo(0.3);
    }

    [Test]
    public async Task Level_DefaultsTo1WhenNothingIsSaved()
    {
        var vm = new VolumeViewModel(Substitute.For<IAudioEngine>(), new SettingsStore(_temp.NewSettingsPath()));

        await Assert.That(vm.Level).IsEqualTo(1.0);
    }

    [Test]
    public async Task SettingLevel_ForwardsToEngine()
    {
        var audioEngine = Substitute.For<IAudioEngine>();
        var vm = new VolumeViewModel(audioEngine, new SettingsStore(_temp.NewSettingsPath()));

        vm.Level = 0.4;

        audioEngine.Received(1).Volume = 0.4f;
    }

    [Test]
    public async Task SettingLevel_WithLogarithmicCurve_AppliesTheSquaredAmplitudeToTheEngine()
    {
        var audioEngine = Substitute.For<IAudioEngine>();
        var vm = new VolumeViewModel(audioEngine, new SettingsStore(_temp.NewSettingsPath()));
        vm.Curve = VolumeCurve.Logarithmic;
        audioEngine.ClearReceivedCalls();

        vm.Level = 0.5;

        audioEngine.Received(1).Volume = 0.25f;
    }

    [Test]
    public async Task SettingCurve_ReappliesTheCurrentLevelToTheEngineImmediately()
    {
        var audioEngine = Substitute.For<IAudioEngine>();
        var vm = new VolumeViewModel(audioEngine, new SettingsStore(_temp.NewSettingsPath()));
        vm.Level = 0.5;
        audioEngine.ClearReceivedCalls();

        vm.Curve = VolumeCurve.Logarithmic;

        audioEngine.Received(1).Volume = 0.25f;
    }
}
