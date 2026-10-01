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
            Substitute.For<IAudioEngine>(), new PlaybackQueue(), CreateTempSettingsPath(), NullLogger<PlaybackControlsViewModel>.Instance);
        return new LibraryViewModel(
            Substitute.For<IFilePickerService>(),
            playbackControls,
            Substitute.For<IFileManagerService>(),
            CreateTempSettingsPath(),
            TestThemeLayout.Default,
            NullLogger<LibraryViewModel>.Instance);
    }

    private static PlaybackSettingsViewModel CreatePlaybackSettingsViewModel()
    {
        var playbackControls = new PlaybackControlsViewModel(
            Substitute.For<IAudioEngine>(), new PlaybackQueue(), CreateTempSettingsPath(), NullLogger<PlaybackControlsViewModel>.Instance);
        return new PlaybackSettingsViewModel(
            CreateTempSettingsPath(),
            Substitute.For<IOutputDeviceService>(),
            playbackControls);
    }

    private static InterfaceSettingsViewModel CreateInterfaceSettingsViewModel() =>
        new(CreateTempSettingsPath(), Substitute.For<IFilePickerService>());

    private static IntegrationsSettingsViewModel CreateIntegrationsSettingsViewModel() => new(CreateTempSettingsPath());

    private static ShortcutSettingsService CreateShortcutSettingsService() => new(CreateTempSettingsPath());

    private static SettingsWindowViewModel CreateViewModel() =>
        new(CreateLibraryViewModel(), CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(), CreateIntegrationsSettingsViewModel(), CreateShortcutSettingsService());

    [Test]
    public async Task Categories_ContainsLibraryPlaybackInterfaceIntegrationsAndKeyboardShortcuts()
    {
        var vm = CreateViewModel();

        await Assert.That(vm.Categories).IsEquivalentTo(
            [Strings.SettingsCategoryLibrary, Strings.SettingsCategoryPlayback, Strings.SettingsCategoryInterface, Strings.SettingsCategoryIntegrations, Strings.SettingsCategoryKeyboardShortcuts]);
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

        var vm = new SettingsWindowViewModel(library, CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(), CreateIntegrationsSettingsViewModel(), CreateShortcutSettingsService());

        await Assert.That(vm.Library).IsEqualTo(library);
    }

    [Test]
    public async Task Playback_ExposesTheSameInstancePassedIn()
    {
        var playback = CreatePlaybackSettingsViewModel();

        var vm = new SettingsWindowViewModel(CreateLibraryViewModel(), playback, CreateInterfaceSettingsViewModel(), CreateIntegrationsSettingsViewModel(), CreateShortcutSettingsService());

        await Assert.That(vm.Playback).IsEqualTo(playback);
    }

    [Test]
    public async Task Interface_ExposesTheSameInstancePassedIn()
    {
        var interfaceSettings = CreateInterfaceSettingsViewModel();

        var vm = new SettingsWindowViewModel(CreateLibraryViewModel(), CreatePlaybackSettingsViewModel(), interfaceSettings, CreateIntegrationsSettingsViewModel(), CreateShortcutSettingsService());

        await Assert.That(vm.Interface).IsEqualTo(interfaceSettings);
    }

    [Test]
    public async Task SelectedCategoryViewModel_AfterSwitchingToIntegrations_ResolvesToIntegrations()
    {
        var integrations = CreateIntegrationsSettingsViewModel();
        var vm = new SettingsWindowViewModel(CreateLibraryViewModel(), CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(), integrations, CreateShortcutSettingsService());

        vm.SelectedCategory = Strings.SettingsCategoryIntegrations;

        await Assert.That(vm.Integrations).IsEqualTo(integrations);
        await Assert.That(vm.SelectedCategoryViewModel).IsEqualTo(integrations);
    }

    [Test]
    public async Task SelectedCategoryViewModel_ResolvesToLibrary()
    {
        var library = CreateLibraryViewModel();

        var vm = new SettingsWindowViewModel(library, CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(), CreateIntegrationsSettingsViewModel(), CreateShortcutSettingsService());

        await Assert.That(vm.SelectedCategoryViewModel).IsEqualTo(library);
    }

    [Test]
    public async Task SelectedCategoryViewModel_AfterSwitchingToPlayback_ResolvesToPlayback()
    {
        var playback = CreatePlaybackSettingsViewModel();
        var vm = new SettingsWindowViewModel(CreateLibraryViewModel(), playback, CreateInterfaceSettingsViewModel(), CreateIntegrationsSettingsViewModel(), CreateShortcutSettingsService());

        vm.SelectedCategory = Strings.SettingsCategoryPlayback;

        await Assert.That(vm.SelectedCategoryViewModel).IsEqualTo(playback);
    }

    [Test]
    public async Task SelectedCategoryViewModel_AfterSwitchingToInterface_ResolvesToInterface()
    {
        var interfaceSettings = CreateInterfaceSettingsViewModel();
        var vm = new SettingsWindowViewModel(CreateLibraryViewModel(), CreatePlaybackSettingsViewModel(), interfaceSettings, CreateIntegrationsSettingsViewModel(), CreateShortcutSettingsService());

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
