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
        var vm = new PlaybackSettingsViewModel(CreateTempSettingsPath(), Substitute.For<IAudioEngine>(), CreatePlaybackControlsViewModel());

        await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo(Strings.SystemDefaultAudioDevice);
        await Assert.That(vm.OutputDeviceDisplayNames).IsEquivalentTo([Strings.SystemDefaultAudioDevice]);
    }

    [Test]
    public async Task SettingSelectedOutputDeviceDisplayName_PersistsAndAppliesToTheEngine()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var audioEngine = Substitute.For<IAudioEngine>();
            var vm = new PlaybackSettingsViewModel(settingsPath, audioEngine, CreatePlaybackControlsViewModel());

            vm.SelectedOutputDeviceDisplayName = "Focusrite Scarlett 2i2";

            audioEngine.Received(1).SetOutputDevice("Focusrite Scarlett 2i2");
            await Assert.That(LibrarySettingsStore.LoadOutputDeviceName(settingsPath)).IsEqualTo("Focusrite Scarlett 2i2");
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
            var audioEngine = Substitute.For<IAudioEngine>();
            var vm = new PlaybackSettingsViewModel(settingsPath, audioEngine, CreatePlaybackControlsViewModel());
            vm.SelectedOutputDeviceDisplayName = "Focusrite Scarlett 2i2";

            vm.SelectedOutputDeviceDisplayName = Strings.SystemDefaultAudioDevice;

            audioEngine.Received(1).SetOutputDevice(null);
            await Assert.That(LibrarySettingsStore.LoadOutputDeviceName(settingsPath)).IsNull();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task Constructor_LoadsAPreviouslySavedDeviceNameWithoutReSavingOrTouchingTheEngine()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveOutputDeviceName(settingsPath, "Focusrite Scarlett 2i2");
            var audioEngine = Substitute.For<IAudioEngine>();

            var vm = new PlaybackSettingsViewModel(settingsPath, audioEngine, CreatePlaybackControlsViewModel());

            await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo("Focusrite Scarlett 2i2");
            audioEngine.DidNotReceive().SetOutputDevice(Arg.Any<string?>());
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
            LibrarySettingsStore.SaveOutputDeviceName(settingsPath, "Focusrite Scarlett 2i2");
            var audioEngine = Substitute.For<IAudioEngine>();
            audioEngine.GetOutputDevices().Returns(
            [
                new AudioOutputDevice("Built-in Speakers", true),
                new AudioOutputDevice("Focusrite Scarlett 2i2", false),
            ]);
            var vm = new PlaybackSettingsViewModel(settingsPath, audioEngine, CreatePlaybackControlsViewModel());

            vm.ApplyPersistedOutputDevice();

            await Assert.That(vm.OutputDeviceDisplayNames).IsEquivalentTo(
                [Strings.SystemDefaultAudioDevice, "Built-in Speakers", "Focusrite Scarlett 2i2"]);
            await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo("Focusrite Scarlett 2i2");
            audioEngine.Received(1).SetOutputDevice("Focusrite Scarlett 2i2");
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
            LibrarySettingsStore.SaveOutputDeviceName(settingsPath, "Unplugged USB DAC");
            var audioEngine = Substitute.For<IAudioEngine>();
            audioEngine.GetOutputDevices().Returns([new AudioOutputDevice("Built-in Speakers", true)]);
            var vm = new PlaybackSettingsViewModel(settingsPath, audioEngine, CreatePlaybackControlsViewModel());

            vm.ApplyPersistedOutputDevice();

            await Assert.That(vm.SelectedOutputDeviceDisplayName).IsEqualTo(Strings.SystemDefaultAudioDevice);
            audioEngine.Received(1).SetOutputDevice(null);
            // The fallback is a display-only affordance - the actual saved preference is left alone
            // in case the device (e.g. a USB DAC) gets plugged back in on a later launch.
            await Assert.That(LibrarySettingsStore.LoadOutputDeviceName(settingsPath)).IsEqualTo("Unplugged USB DAC");
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task UseLogarithmicVolumeScale_DefaultsToFalse()
    {
        var vm = new PlaybackSettingsViewModel(CreateTempSettingsPath(), Substitute.For<IAudioEngine>(), CreatePlaybackControlsViewModel());

        await Assert.That(vm.UseLogarithmicVolumeScale).IsFalse();
    }

    [Test]
    public async Task SettingUseLogarithmicVolumeScale_PersistsAndAppliesToPlaybackControls()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var playbackControls = CreatePlaybackControlsViewModel();
            var vm = new PlaybackSettingsViewModel(settingsPath, Substitute.For<IAudioEngine>(), playbackControls);

            vm.UseLogarithmicVolumeScale = true;

            await Assert.That(playbackControls.VolumeCurve).IsEqualTo(VolumeCurve.Logarithmic);
            await Assert.That(LibrarySettingsStore.LoadVolumeCurve(settingsPath)).IsEqualTo(VolumeCurve.Logarithmic);
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
            LibrarySettingsStore.SaveVolumeCurve(settingsPath, VolumeCurve.Logarithmic);
            var playbackControls = CreatePlaybackControlsViewModel();

            var vm = new PlaybackSettingsViewModel(settingsPath, Substitute.For<IAudioEngine>(), playbackControls);

            await Assert.That(vm.UseLogarithmicVolumeScale).IsTrue();
            await Assert.That(playbackControls.VolumeCurve).IsEqualTo(VolumeCurve.Logarithmic);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }
}
