using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.Shortcuts;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class ShortcutRegistryTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-shortcut-registry-tests-{Guid.NewGuid():N}.json");

    private static readonly int[] StandardEqualizerFrequencies = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    private static MainWindowViewModel CreateMainWindowViewModel()
    {
        var audioEngine = Substitute.For<IAudioEngine>();
        audioEngine.EqualizerBandFrequenciesHz.Returns(StandardEqualizerFrequencies);
        audioEngine.EqualizerBandGainsDb.Returns(new float[10]);
        return new MainWindowViewModel(
            audioEngine,
            Substitute.For<IOutputDeviceService>(),
            Substitute.For<IFilePickerService>(),
            Substitute.For<IFileManagerService>(),
            CreateTempSettingsPath(),
            NullLogger<PlaybackControlsViewModel>.Instance,
            NullLogger<LibraryViewModel>.Instance,
            NullLogger<QueueViewModel>.Instance);
    }

    [Test]
    public async Task All_HasNoDuplicateIds()
    {
        await Assert.That(ShortcutRegistry.All.Select(s => s.Id).Distinct().Count()).IsEqualTo(ShortcutRegistry.All.Count);
    }

    [Test]
    public async Task All_HasNoDuplicateDefaultGestures()
    {
        await Assert.That(ShortcutRegistry.All.Select(s => s.DefaultGesture).Distinct().Count()).IsEqualTo(ShortcutRegistry.All.Count);
    }

    [Test]
    public async Task IncreaseTileSizeShortcut_TargetsWhicheverGridIsActiveAtTheTimeItFires()
    {
        var mainWindowViewModel = CreateMainWindowViewModel();
        var vm = mainWindowViewModel.Library;
        var increaseTileSize = ShortcutRegistry.All.Single(s => s.Id == "Library.IncreaseTileSize");
        var command = increaseTileSize.Command(mainWindowViewModel);

        vm.LibrarySection = LibrarySection.Library;
        var libraryTileSizeBefore = vm.Grid.TileSize;
        command.Execute(null);
        await Assert.That(vm.Grid.TileSize).IsGreaterThan(libraryTileSizeBefore);

        vm.LibrarySection = LibrarySection.RecentlyAdded;
        var recentlyAddedTileSizeBefore = vm.RecentlyAddedGrid.TileSize;
        command.Execute(null);
        await Assert.That(vm.RecentlyAddedGrid.TileSize).IsGreaterThan(recentlyAddedTileSizeBefore);

        vm.LibrarySection = LibrarySection.Playlist;
        var libraryTileSizeNow = vm.Grid.TileSize;
        var recentlyAddedTileSizeNow = vm.RecentlyAddedGrid.TileSize;
        command.Execute(null); // must no-op - no Cover Art view exists for a playlist
        await Assert.That(vm.Grid.TileSize).IsEqualTo(libraryTileSizeNow);
        await Assert.That(vm.RecentlyAddedGrid.TileSize).IsEqualTo(recentlyAddedTileSizeNow);
    }
}
