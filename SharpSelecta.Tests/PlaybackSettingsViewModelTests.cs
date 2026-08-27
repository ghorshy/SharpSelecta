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
        new(audioEngine ?? Substitute.For<IAudioEngine>(), new PlaybackQueue(), NullLogger<PlaybackControlsViewModel>.Instance);

    [Test]
    public async Task SelectedOutputDeviceDisplayName_DefaultsToSystemDefault()
    {
        var vm = new PlaybackSettingsViewModel(CreateTempSettingsPath(), Substitute.For<IOutputDeviceService>(), CreatePlaybackControlsViewModel());

        await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo(Strings.SystemDefaultAudioDevice);
        await Assert.That(vm.OutputDeviceDisplayNames).IsEquivalentTo([Strings.SystemDefaultAudioDevice]);
    }

    [Test]
    public async Task SettingSelectedOutputDeviceDisplayName_PersistsAndAppliesToTheService()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var outputDeviceService = Substitute.For<IOutputDeviceService>();
            var vm = new PlaybackSettingsViewModel(settingsPath, outputDeviceService, CreatePlaybackControlsViewModel());

            vm.SelectedOutputDeviceDisplayName = "Focusrite Scarlett 2i2";
            await vm.OutputDeviceSwitchTask;

            await outputDeviceService.Received(1).SetOutputDeviceAsync("Focusrite Scarlett 2i2");
            await Assert.That(SettingsStore.LoadOutputDeviceName(settingsPath)).IsEqualTo("Focusrite Scarlett 2i2");
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SettingSelectedOutputDeviceDisplayName_BackToSystemDefault_PersistsNull()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var outputDeviceService = Substitute.For<IOutputDeviceService>();
            var vm = new PlaybackSettingsViewModel(settingsPath, outputDeviceService, CreatePlaybackControlsViewModel());
            vm.SelectedOutputDeviceDisplayName = "Focusrite Scarlett 2i2";
            await vm.OutputDeviceSwitchTask;

            vm.SelectedOutputDeviceDisplayName = Strings.SystemDefaultAudioDevice;
            await vm.OutputDeviceSwitchTask;

            await outputDeviceService.Received(1).SetOutputDeviceAsync(null);
            await Assert.That(SettingsStore.LoadOutputDeviceName(settingsPath)).IsNull();
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
            SettingsStore.SaveOutputDeviceName(settingsPath, "Focusrite Scarlett 2i2");
            var outputDeviceService = Substitute.For<IOutputDeviceService>();

            var vm = new PlaybackSettingsViewModel(settingsPath, outputDeviceService, CreatePlaybackControlsViewModel());

            await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo("Focusrite Scarlett 2i2");
            await outputDeviceService.DidNotReceive().SetOutputDeviceAsync(Arg.Any<string?>());
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
            SettingsStore.SaveOutputDeviceName(settingsPath, "Focusrite Scarlett 2i2");
            var outputDeviceService = Substitute.For<IOutputDeviceService>();
            outputDeviceService.GetOutputDevicesAsync().Returns(Task.FromResult<IReadOnlyList<AudioOutputDevice>>(
            [
                new AudioOutputDevice("Built-in Speakers", true),
                new AudioOutputDevice("Focusrite Scarlett 2i2", false),
            ]));
            var vm = new PlaybackSettingsViewModel(settingsPath, outputDeviceService, CreatePlaybackControlsViewModel());

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
            SettingsStore.SaveOutputDeviceName(settingsPath, "Unplugged USB DAC");
            var outputDeviceService = Substitute.For<IOutputDeviceService>();
            outputDeviceService.GetOutputDevicesAsync().Returns(Task.FromResult<IReadOnlyList<AudioOutputDevice>>(
                [new AudioOutputDevice("Built-in Speakers", true)]));
            var vm = new PlaybackSettingsViewModel(settingsPath, outputDeviceService, CreatePlaybackControlsViewModel());

            await vm.ApplyPersistedOutputDeviceAsync();

            await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo(Strings.SystemDefaultAudioDevice);
            await outputDeviceService.DidNotReceive().SetOutputDeviceAsync(Arg.Any<string?>());
            await Assert.That(SettingsStore.LoadOutputDeviceName(settingsPath)).IsEqualTo("Unplugged USB DAC");
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task UseLogarithmicVolumeScale_DefaultsToFalse()
    {
        var vm = new PlaybackSettingsViewModel(CreateTempSettingsPath(), Substitute.For<IOutputDeviceService>(), CreatePlaybackControlsViewModel());

        await Assert.That(vm.UseLogarithmicVolumeScale).IsFalse();
    }

    [Test]
    public async Task SettingUseLogarithmicVolumeScale_PersistsAndAppliesToPlaybackControls()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var playbackControls = CreatePlaybackControlsViewModel();
            var vm = new PlaybackSettingsViewModel(settingsPath, Substitute.For<IOutputDeviceService>(), playbackControls);

            vm.UseLogarithmicVolumeScale = true;

            await Assert.That(playbackControls.VolumeCurve).IsEqualTo(VolumeCurve.Logarithmic);
            await Assert.That(SettingsStore.LoadVolumeCurve(settingsPath)).IsEqualTo(VolumeCurve.Logarithmic);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task Constructor_AppliesAPreviouslySavedVolumeCurveToPlaybackControls()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            SettingsStore.SaveVolumeCurve(settingsPath, VolumeCurve.Logarithmic);
            var playbackControls = CreatePlaybackControlsViewModel();

            var vm = new PlaybackSettingsViewModel(settingsPath, Substitute.For<IOutputDeviceService>(), playbackControls);

            await Assert.That(vm.UseLogarithmicVolumeScale).IsTrue();
            await Assert.That(playbackControls.VolumeCurve).IsEqualTo(VolumeCurve.Logarithmic);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SeekStepSeconds_DefaultsToFive()
    {
        var vm = new PlaybackSettingsViewModel(CreateTempSettingsPath(), Substitute.For<IOutputDeviceService>(), CreatePlaybackControlsViewModel());

        await Assert.That(vm.SeekStepSeconds).IsEqualTo(5);
    }

    [Test]
    public async Task SettingSeekStepSeconds_PersistsAndAppliesToPlaybackControls()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var playbackControls = CreatePlaybackControlsViewModel();
            var vm = new PlaybackSettingsViewModel(settingsPath, Substitute.For<IOutputDeviceService>(), playbackControls);

            vm.SeekStepSeconds = 8;

            await Assert.That(playbackControls.SeekStepSeconds).IsEqualTo(8);
            await Assert.That(SettingsStore.LoadSeekStepSeconds(settingsPath)).IsEqualTo(8);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task Constructor_AppliesAPreviouslySavedSeekStepSecondsToPlaybackControls()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            SettingsStore.SaveSeekStepSeconds(settingsPath, 3);
            var playbackControls = CreatePlaybackControlsViewModel();

            var vm = new PlaybackSettingsViewModel(settingsPath, Substitute.For<IOutputDeviceService>(), playbackControls);

            await Assert.That(vm.SeekStepSeconds).IsEqualTo(3);
            await Assert.That(playbackControls.SeekStepSeconds).IsEqualTo(3);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }
}
