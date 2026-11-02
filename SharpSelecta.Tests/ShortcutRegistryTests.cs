using System.Linq;
using SharpSelecta.App.Shortcuts;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class ShortcutRegistryTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-shortcut-registry-tests-{Guid.NewGuid():N}.json");

    private static MainWindowViewModel CreateMainWindowViewModel() =>
        TestMainWindowViewModel.Create(CreateTempSettingsPath(), out _);

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
    public async Task RefreshShortcut_DefaultsToF5_AndRescansTheLibrary()
    {
        var mainWindowViewModel = CreateMainWindowViewModel();
        var refresh = ShortcutRegistry.All.Single(s => s.Id == "Library.Refresh");

        await Assert.That(refresh.DefaultGesture).IsEqualTo("F5");
        await Assert.That(refresh.Command(mainWindowViewModel)).IsSameReferenceAs(mainWindowViewModel.Library.RescanCommand);
    }

    [Test]
    public async Task PropertiesShortcut_DefaultsToAltEnter_AndAsksTheViewToOpenTheFocusedTrack()
    {
        var mainWindowViewModel = CreateMainWindowViewModel();
        var properties = ShortcutRegistry.All.Single(s => s.Id == "Library.Properties");
        var requested = 0;
        mainWindowViewModel.OpenPropertiesRequested += (_, _) => requested++;

        properties.Command(mainWindowViewModel).Execute(null);

        await Assert.That(properties.DefaultGesture).IsEqualTo("Alt+Enter");
        await Assert.That(requested).IsEqualTo(1);
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
