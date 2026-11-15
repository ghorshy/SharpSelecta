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

    private VolumeViewModel CreateAt(double level) =>
        new(Substitute.For<IAudioEngine>(), new SettingsStore(_temp.NewSettingsPath())) { Level = level };

    [Test]
    public async Task VolumeUpAndDown_StepTheSliderPosition_AndStopAtTheEnds()
    {
        var vm = CreateAt(0.5);

        vm.VolumeUpCommand.Execute(null);
        await Assert.That(vm.Level).IsEqualTo(0.55);

        vm.VolumeDownCommand.Execute(null);
        vm.VolumeDownCommand.Execute(null);
        await Assert.That(vm.Level).IsEqualTo(0.45);

        vm.Level = 0.98;
        vm.VolumeUpCommand.Execute(null);
        await Assert.That(vm.Level).IsEqualTo(1.0);

        vm.Level = 0.02;
        vm.VolumeDownCommand.Execute(null);
        await Assert.That(vm.Level).IsEqualTo(0.0);
    }

    [Test]
    public async Task ToggleMute_SilencesAndThenRestoresTheLevelFromBefore()
    {
        var vm = CreateAt(0.7);

        vm.ToggleMuteCommand.Execute(null);
        await Assert.That(vm.Level).IsEqualTo(0.0);

        vm.ToggleMuteCommand.Execute(null);
        await Assert.That(vm.Level).IsEqualTo(0.7);
    }

    [Test]
    public async Task MovingTheSliderWhileMuted_ForgetsTheLevelFromBeforeMute()
    {
        var vm = CreateAt(0.7);
        vm.ToggleMuteCommand.Execute(null);

        vm.Level = 0.2;
        vm.ToggleMuteCommand.Execute(null);

        await Assert.That(vm.Level).IsEqualTo(0.0);
        vm.ToggleMuteCommand.Execute(null);
        await Assert.That(vm.Level).IsEqualTo(0.2);
    }

    [Test]
    public async Task VolumeUpWhileMuted_UnmutesOneStepAboveTheLevelFromBefore()
    {
        var vm = CreateAt(0.5);
        vm.ToggleMuteCommand.Execute(null);

        vm.VolumeUpCommand.Execute(null);

        await Assert.That(vm.Level).IsEqualTo(0.55);
    }

    [Test]
    public async Task Persist_WhileMuted_SavesTheLevelFromBeforeMute()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = new VolumeViewModel(Substitute.For<IAudioEngine>(), new SettingsStore(settingsPath)) { Level = 0.6 };
        vm.ToggleMuteCommand.Execute(null);

        vm.Persist();

        await Assert.That(new SettingsStore(settingsPath).Current.Volume).IsEqualTo(0.6);
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
