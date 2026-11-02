using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class PlaybackSettingsViewModelTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-playback-settings-vm-tests-{Guid.NewGuid():N}.json");

    private static PlaybackControlsViewModel CreatePlaybackControlsViewModel(IAudioEngine? audioEngine = null) =>
        new(audioEngine ?? Substitute.For<IAudioEngine>(), new PlaybackQueue(), new SettingsStore(CreateTempSettingsPath()), NullLogger<PlaybackControlsViewModel>.Instance);

    [Test]
    public async Task SelectedOutputDeviceDisplayName_DefaultsToSystemDefault()
    {
        var vm = new PlaybackSettingsViewModel(new SettingsStore(CreateTempSettingsPath()), Substitute.For<IOutputDeviceService>(), CreatePlaybackControlsViewModel());

        await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo(Strings.SystemDefaultAudioDevice);
        await Assert.That(vm.OutputDeviceDisplayNames).IsEquivalentTo([Strings.SystemDefaultAudioDevice]);
    }

    [Test]
    public async Task EditingSettings_DoesNotPersistOrApplyAnythingUntilApplyIsClicked()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var outputDeviceService = Substitute.For<IOutputDeviceService>();
            var playbackControls = CreatePlaybackControlsViewModel();
            var vm = new PlaybackSettingsViewModel(new SettingsStore(settingsPath), outputDeviceService, playbackControls);
            var settingsVm = (ISettingsCategoryViewModel)vm;

            vm.SelectedOutputDeviceDisplayName = "Focusrite Scarlett 2i2";
            vm.UseLogarithmicVolumeScale = true;
            vm.SeekStepSeconds = 8;
            vm.UseWaveformSlider = true;
            vm.RestoreQueueOnStartup = false; // defaults to true, so this is a genuine pending change

            await Assert.That(settingsVm.HasPendingChanges).IsTrue();
            await outputDeviceService.DidNotReceive().SetOutputDeviceAsync(Arg.Any<string?>());
            await Assert.That(new SettingsStore(settingsPath).Current.OutputDeviceName).IsNull();
            await Assert.That(new SettingsStore(settingsPath).Current.VolumeCurve).IsEqualTo(VolumeCurve.Linear);
            await Assert.That(new SettingsStore(settingsPath).Current.SeekStepSeconds).IsEqualTo(5);
            await Assert.That(playbackControls.SeekStepSeconds).IsEqualTo(5);
            await Assert.That(playbackControls.Volume.Curve).IsEqualTo(VolumeCurve.Linear);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task ApplyCommand_PersistsAndAppliesEveryPendingSetting()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var outputDeviceService = Substitute.For<IOutputDeviceService>();
            var playbackControls = CreatePlaybackControlsViewModel();
            var vm = new PlaybackSettingsViewModel(new SettingsStore(settingsPath), outputDeviceService, playbackControls);
            var settingsVm = (ISettingsCategoryViewModel)vm;

            vm.SelectedOutputDeviceDisplayName = "Focusrite Scarlett 2i2";
            vm.UseLogarithmicVolumeScale = true;
            vm.SeekStepSeconds = 8;
            vm.UseWaveformSlider = true;
            vm.RestoreQueueOnStartup = false; // defaults to true, so this is a genuine change to persist

            settingsVm.ApplyCommand.Execute(null);
            await vm.OutputDeviceSwitchTask;

            await outputDeviceService.Received(1).SetOutputDeviceAsync("Focusrite Scarlett 2i2");
            await Assert.That(new SettingsStore(settingsPath).Current.OutputDeviceName).IsEqualTo("Focusrite Scarlett 2i2");
            await Assert.That(new SettingsStore(settingsPath).Current.VolumeCurve).IsEqualTo(VolumeCurve.Logarithmic);
            await Assert.That(playbackControls.Volume.Curve).IsEqualTo(VolumeCurve.Logarithmic);
            await Assert.That(new SettingsStore(settingsPath).Current.SeekStepSeconds).IsEqualTo(8);
            await Assert.That(playbackControls.SeekStepSeconds).IsEqualTo(8);
            await Assert.That(new SettingsStore(settingsPath).Current.UseWaveformSlider).IsTrue();
            await Assert.That(playbackControls.Waveform.UseSlider).IsTrue();
            await Assert.That(new SettingsStore(settingsPath).Current.RestoreQueueOnStartup).IsFalse();
            await Assert.That(settingsVm.HasPendingChanges).IsFalse();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task CancelCommand_RevertsEveryEditedSettingWithoutPersistingOrApplyingAnything()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var outputDeviceService = Substitute.For<IOutputDeviceService>();
            var playbackControls = CreatePlaybackControlsViewModel();
            var vm = new PlaybackSettingsViewModel(new SettingsStore(settingsPath), outputDeviceService, playbackControls);
            var settingsVm = (ISettingsCategoryViewModel)vm;

            vm.SelectedOutputDeviceDisplayName = "Focusrite Scarlett 2i2";
            vm.UseLogarithmicVolumeScale = true;
            vm.SeekStepSeconds = 8;
            vm.UseWaveformSlider = true;
            vm.RestoreQueueOnStartup = false; // defaults to true, so this is a genuine change to revert

            settingsVm.CancelCommand.Execute(null);

            await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo(Strings.SystemDefaultAudioDevice);
            await Assert.That(vm.UseLogarithmicVolumeScale).IsFalse();
            await Assert.That(vm.SeekStepSeconds).IsEqualTo(5);
            await Assert.That(vm.UseWaveformSlider).IsFalse();
            await Assert.That(vm.RestoreQueueOnStartup).IsTrue();
            await Assert.That(settingsVm.HasPendingChanges).IsFalse();
            await outputDeviceService.DidNotReceive().SetOutputDeviceAsync(Arg.Any<string?>());
            await Assert.That(new SettingsStore(settingsPath).Current.OutputDeviceName).IsNull();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task Constructor_LoadsAPreviouslySavedDeviceNameWithoutReSavingOrTouchingTheService()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            new SettingsStore(settingsPath).Update(s => s with { OutputDeviceName = "Focusrite Scarlett 2i2" });
            var outputDeviceService = Substitute.For<IOutputDeviceService>();

            var vm = new PlaybackSettingsViewModel(new SettingsStore(settingsPath), outputDeviceService, CreatePlaybackControlsViewModel());

            await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo("Focusrite Scarlett 2i2");
            await outputDeviceService.DidNotReceive().SetOutputDeviceAsync(Arg.Any<string?>());
            await Assert.That(((ISettingsCategoryViewModel)vm).HasPendingChanges).IsFalse();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task ApplyPersistedOutputDevice_PopulatesTheDeviceListAndReSelectsTheSavedDevice()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            new SettingsStore(settingsPath).Update(s => s with { OutputDeviceName = "Focusrite Scarlett 2i2" });
            var outputDeviceService = Substitute.For<IOutputDeviceService>();
            outputDeviceService.GetOutputDevicesAsync().Returns(Task.FromResult<IReadOnlyList<AudioOutputDevice>>(
            [
                new AudioOutputDevice("Built-in Speakers", true),
                new AudioOutputDevice("Focusrite Scarlett 2i2", false),
            ]));
            var vm = new PlaybackSettingsViewModel(new SettingsStore(settingsPath), outputDeviceService, CreatePlaybackControlsViewModel());

            await vm.ApplyPersistedOutputDeviceAsync();

            await Assert.That(vm.OutputDeviceDisplayNames).IsEquivalentTo(
                [Strings.SystemDefaultAudioDevice, "Built-in Speakers", "Focusrite Scarlett 2i2"]);
            await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo("Focusrite Scarlett 2i2");
            await outputDeviceService.Received(1).SetOutputDeviceAsync("Focusrite Scarlett 2i2");
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task ApplyPersistedOutputDevice_WhenTheSavedDeviceIsNoLongerPresent_FallsBackToSystemDefaultWithoutOverwritingTheSavedPreference()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            new SettingsStore(settingsPath).Update(s => s with { OutputDeviceName = "Unplugged USB DAC" });
            var outputDeviceService = Substitute.For<IOutputDeviceService>();
            outputDeviceService.GetOutputDevicesAsync().Returns(Task.FromResult<IReadOnlyList<AudioOutputDevice>>(
                [new AudioOutputDevice("Built-in Speakers", true)]));
            var vm = new PlaybackSettingsViewModel(new SettingsStore(settingsPath), outputDeviceService, CreatePlaybackControlsViewModel());

            await vm.ApplyPersistedOutputDeviceAsync();

            await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo(Strings.SystemDefaultAudioDevice);
            await outputDeviceService.DidNotReceive().SetOutputDeviceAsync(Arg.Any<string?>());
            await Assert.That(new SettingsStore(settingsPath).Current.OutputDeviceName).IsEqualTo("Unplugged USB DAC");
            await Assert.That(((ISettingsCategoryViewModel)vm).HasPendingChanges).IsFalse();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task UseLogarithmicVolumeScale_DefaultsToFalse()
    {
        var vm = new PlaybackSettingsViewModel(new SettingsStore(CreateTempSettingsPath()), Substitute.For<IOutputDeviceService>(), CreatePlaybackControlsViewModel());

        await Assert.That(vm.UseLogarithmicVolumeScale).IsFalse();
    }

    [Test]
    public async Task Constructor_ReflectsAPreviouslySavedVolumeCurve()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            new SettingsStore(settingsPath).Update(s => s with { VolumeCurve = VolumeCurve.Logarithmic });

            var vm = new PlaybackSettingsViewModel(new SettingsStore(settingsPath), Substitute.For<IOutputDeviceService>(), CreatePlaybackControlsViewModel());

            await Assert.That(vm.UseLogarithmicVolumeScale).IsTrue();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SeekStepSeconds_DefaultsToFive()
    {
        var vm = new PlaybackSettingsViewModel(new SettingsStore(CreateTempSettingsPath()), Substitute.For<IOutputDeviceService>(), CreatePlaybackControlsViewModel());

        await Assert.That(vm.SeekStepSeconds).IsEqualTo(5);
    }

    [Test]
    public async Task Constructor_AppliesAPreviouslySavedSeekStepSecondsToPlaybackControls()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            new SettingsStore(settingsPath).Update(s => s with { SeekStepSeconds = 3 });
            var playbackControls = CreatePlaybackControlsViewModel();

            var vm = new PlaybackSettingsViewModel(new SettingsStore(settingsPath), Substitute.For<IOutputDeviceService>(), playbackControls);

            await Assert.That(vm.SeekStepSeconds).IsEqualTo(3);
            await Assert.That(playbackControls.SeekStepSeconds).IsEqualTo(3);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task Constructor_ReflectsASavedUseWaveformSlider()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            new SettingsStore(settingsPath).Update(s => s with { UseWaveformSlider = true });

            var vm = new PlaybackSettingsViewModel(new SettingsStore(settingsPath), Substitute.For<IOutputDeviceService>(), CreatePlaybackControlsViewModel());

            await Assert.That(vm.UseWaveformSlider).IsTrue();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }
}
