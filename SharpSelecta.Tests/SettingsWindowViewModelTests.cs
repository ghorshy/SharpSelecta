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
    private static LibraryViewModel CreateLibraryViewModel()
    {
        var playbackControls = new PlaybackControlsViewModel(
            Substitute.For<IAudioEngine>(), new PlaybackQueue(), NullLogger<PlaybackControlsViewModel>.Instance);
        return new LibraryViewModel(
            Substitute.For<IFilePickerService>(),
            playbackControls,
            Path.Combine(Path.GetTempPath(), $"sharpselecta-settings-vm-tests-{Guid.NewGuid():N}.json"),
            NullLogger<LibraryViewModel>.Instance);
    }

    [Test]
    public async Task Categories_ContainsLibraryAsTheOnlyEntrySoFar()
    {
        var vm = new SettingsWindowViewModel(CreateLibraryViewModel());

        await Assert.That(vm.Categories).IsEquivalentTo([Strings.SettingsCategoryLibrary]);
    }

    [Test]
    public async Task SelectedCategory_DefaultsToTheFirstCategory()
    {
        var vm = new SettingsWindowViewModel(CreateLibraryViewModel());

        await Assert.That(vm.SelectedCategory).IsEqualTo(Strings.SettingsCategoryLibrary);
    }

    [Test]
    public async Task Library_ExposesTheSameInstancePassedIn()
    {
        var library = CreateLibraryViewModel();

        var vm = new SettingsWindowViewModel(library);

        await Assert.That(vm.Library).IsEqualTo(library);
    }

    [Test]
    public async Task SelectedCategoryViewModel_ResolvesToLibrary()
    {
        var library = CreateLibraryViewModel();

        var vm = new SettingsWindowViewModel(library);

        await Assert.That(vm.SelectedCategoryViewModel).IsEqualTo(library);
    }
}
