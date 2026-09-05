using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class SettingsWindowViewModelTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-settings-vm-tests-{Guid.NewGuid():N}.json");

    private static LibraryViewModel CreateLibraryViewModel()
    {
        var playbackControls = new PlaybackControlsViewModel(
            Substitute.For<IAudioEngine>(), new PlaybackQueue(), NullLogger<PlaybackControlsViewModel>.Instance);
        return new LibraryViewModel(
            Substitute.For<IFilePickerService>(),
            playbackControls,
            Substitute.For<IFileManagerService>(),
            CreateTempSettingsPath(),
            NullLogger<LibraryViewModel>.Instance);
    }

    private static PlaybackSettingsViewModel CreatePlaybackSettingsViewModel()
    {
        var playbackControls = new PlaybackControlsViewModel(
            Substitute.For<IAudioEngine>(), new PlaybackQueue(), NullLogger<PlaybackControlsViewModel>.Instance);
        return new PlaybackSettingsViewModel(
            CreateTempSettingsPath(),
            Substitute.For<IOutputDeviceService>(),
            playbackControls);
    }

    private static InterfaceSettingsViewModel CreateInterfaceSettingsViewModel() => new(CreateTempSettingsPath());

    private static ShortcutSettingsService CreateShortcutSettingsService() => new(CreateTempSettingsPath());

    private static SettingsWindowViewModel CreateViewModel() =>
        new(CreateLibraryViewModel(), CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(), CreateShortcutSettingsService());

    [Test]
    public async Task Categories_ContainsLibraryPlaybackInterfaceAndKeyboardShortcuts()
    {
        var vm = CreateViewModel();

        await Assert.That(vm.Categories).IsEquivalentTo(
            [Strings.SettingsCategoryLibrary, Strings.SettingsCategoryPlayback, Strings.SettingsCategoryInterface, Strings.SettingsCategoryKeyboardShortcuts]);
    }

    [Test]
    public async Task SelectedCategory_DefaultsToTheFirstCategory()
    {
        var vm = CreateViewModel();

        await Assert.That(vm.SelectedCategory).IsEqualTo(Strings.SettingsCategoryLibrary);
    }

    [Test]
    public async Task Library_ExposesTheSameInstancePassedIn()
    {
        var library = CreateLibraryViewModel();

        var vm = new SettingsWindowViewModel(library, CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(), CreateShortcutSettingsService());

        await Assert.That(vm.Library).IsEqualTo(library);
    }

    [Test]
    public async Task Playback_ExposesTheSameInstancePassedIn()
    {
        var playback = CreatePlaybackSettingsViewModel();

        var vm = new SettingsWindowViewModel(CreateLibraryViewModel(), playback, CreateInterfaceSettingsViewModel(), CreateShortcutSettingsService());

        await Assert.That(vm.Playback).IsEqualTo(playback);
    }

    [Test]
    public async Task Interface_ExposesTheSameInstancePassedIn()
    {
        var interfaceSettings = CreateInterfaceSettingsViewModel();

        var vm = new SettingsWindowViewModel(CreateLibraryViewModel(), CreatePlaybackSettingsViewModel(), interfaceSettings, CreateShortcutSettingsService());

        await Assert.That(vm.Interface).IsEqualTo(interfaceSettings);
    }

    [Test]
    public async Task SelectedCategoryViewModel_ResolvesToLibrary()
    {
        var library = CreateLibraryViewModel();

        var vm = new SettingsWindowViewModel(library, CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(), CreateShortcutSettingsService());

        await Assert.That(vm.SelectedCategoryViewModel).IsEqualTo(library);
    }

    [Test]
    public async Task SelectedCategoryViewModel_AfterSwitchingToPlayback_ResolvesToPlayback()
    {
        var playback = CreatePlaybackSettingsViewModel();
        var vm = new SettingsWindowViewModel(CreateLibraryViewModel(), playback, CreateInterfaceSettingsViewModel(), CreateShortcutSettingsService());

        vm.SelectedCategory = Strings.SettingsCategoryPlayback;

        await Assert.That(vm.SelectedCategoryViewModel).IsEqualTo(playback);
    }

    [Test]
    public async Task SelectedCategoryViewModel_AfterSwitchingToInterface_ResolvesToInterface()
    {
        var interfaceSettings = CreateInterfaceSettingsViewModel();
        var vm = new SettingsWindowViewModel(CreateLibraryViewModel(), CreatePlaybackSettingsViewModel(), interfaceSettings, CreateShortcutSettingsService());

        vm.SelectedCategory = Strings.SettingsCategoryInterface;

        await Assert.That(vm.SelectedCategoryViewModel).IsEqualTo(interfaceSettings);
    }

    [Test]
    public async Task SelectedCategoryViewModel_AfterSwitchingToKeyboardShortcuts_ResolvesToKeyboardShortcuts()
    {
        var vm = CreateViewModel();

        vm.SelectedCategory = Strings.SettingsCategoryKeyboardShortcuts;

        await Assert.That(vm.SelectedCategoryViewModel).IsEqualTo(vm.KeyboardShortcuts);
    }
}
