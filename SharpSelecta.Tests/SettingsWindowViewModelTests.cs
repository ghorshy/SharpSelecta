using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class SettingsWindowViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private LibraryViewModel CreateLibraryViewModel()
    {
        var playbackControls = new PlaybackControlsViewModel(
            Substitute.For<IAudioEngine>(), new PlaybackQueue(), new SettingsStore(_temp.NewSettingsPath()), NullLogger<PlaybackControlsViewModel>.Instance);
        return new LibraryViewModel(
            Substitute.For<IFilePickerService>(),
            playbackControls,
            Substitute.For<IFileManagerService>(),
            new SettingsStore(_temp.NewSettingsPath()),
            TestThemeLayout.Default,
            NullLogger<LibraryViewModel>.Instance);
    }

    private PlaybackSettingsViewModel CreatePlaybackSettingsViewModel()
    {
        var playbackControls = new PlaybackControlsViewModel(
            Substitute.For<IAudioEngine>(), new PlaybackQueue(), new SettingsStore(_temp.NewSettingsPath()), NullLogger<PlaybackControlsViewModel>.Instance);
        return new PlaybackSettingsViewModel(
            new SettingsStore(_temp.NewSettingsPath()),
            Substitute.For<IOutputDeviceService>(),
            playbackControls);
    }

    private InterfaceSettingsViewModel CreateInterfaceSettingsViewModel() =>
        new(new SettingsStore(_temp.NewSettingsPath()), Substitute.For<IFilePickerService>());

    private IntegrationsSettingsViewModel CreateIntegrationsSettingsViewModel() => new(new SettingsStore(_temp.NewSettingsPath()));

    private ShortcutSettingsService CreateShortcutSettingsService() => new(new SettingsStore(_temp.NewSettingsPath()));

    private SettingsWindowViewModel CreateViewModel() =>
        new(CreateLibraryViewModel(), CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(), CreateIntegrationsSettingsViewModel(), CreateShortcutSettingsService());

    [Test]
    public async Task Categories_AreTheCategoryViewModelsInOrder_NamedForTheList()
    {
        var (library, playback, interfaceSettings, integrations) =
            (CreateLibraryViewModel(), CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(), CreateIntegrationsSettingsViewModel());

        var vm = new SettingsWindowViewModel(library, playback, interfaceSettings, integrations, CreateShortcutSettingsService());

        await Assert.That(vm.Categories[0]).IsSameReferenceAs(library);
        await Assert.That(vm.Categories[1]).IsSameReferenceAs(playback);
        await Assert.That(vm.Categories[2]).IsSameReferenceAs(interfaceSettings);
        await Assert.That(vm.Categories[3]).IsSameReferenceAs(integrations);
        await Assert.That(vm.Categories[4]).IsSameReferenceAs(vm.KeyboardShortcuts);
        await Assert.That(string.Join("|", vm.Categories.Select(c => c.DisplayName))).IsEqualTo(string.Join("|",
            Strings.SettingsCategoryLibrary, Strings.SettingsCategoryPlayback, Strings.SettingsCategoryInterface,
            Strings.SettingsCategoryIntegrations, Strings.SettingsCategoryKeyboardShortcuts));
    }

    [Test]
    public async Task SelectedCategory_DefaultsToLibrary()
    {
        var library = CreateLibraryViewModel();

        var vm = new SettingsWindowViewModel(library, CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(), CreateIntegrationsSettingsViewModel(), CreateShortcutSettingsService());

        await Assert.That(vm.SelectedCategory).IsSameReferenceAs(library);
    }

    [Test]
    public async Task Categories_IncludeUpdates_OnlyWhenThisInstallCanUpdateItself()
    {
        var service = Substitute.For<SharpSelecta.Core.Updates.IUpdateService>();
        service.IsSupported.Returns(true);
        var updates = new UpdatesViewModel(new SettingsStore(_temp.NewSettingsPath()), service, NullLogger.Instance);

        var vm = new SettingsWindowViewModel(CreateLibraryViewModel(), CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(),
            CreateIntegrationsSettingsViewModel(), CreateShortcutSettingsService(), updates);

        await Assert.That(vm.Categories[^1]).IsSameReferenceAs(updates);
        await Assert.That(updates.DisplayName).IsEqualTo(Strings.SettingsCategoryUpdates);

        var unsupported = new SettingsWindowViewModel(CreateLibraryViewModel(), CreatePlaybackSettingsViewModel(), CreateInterfaceSettingsViewModel(),
            CreateIntegrationsSettingsViewModel(), CreateShortcutSettingsService(), new UpdatesViewModel(new SettingsStore(_temp.NewSettingsPath()), null, NullLogger.Instance));

        await Assert.That(unsupported.Categories.OfType<UpdatesViewModel>().Any()).IsFalse();
    }
}
