using System.Linq;
using System.Windows.Input;
using SharpSelecta.App.Shortcuts;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class ShortcutRegistryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainWindowViewModel CreateMainWindowViewModel() =>
        TestMainWindowViewModel.Create(_temp.NewSettingsPath(), out _);

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
    public async Task FullScreenShortcut_DefaultsToF11_AndAsksTheWindowToToggle()
    {
        var mainWindowViewModel = CreateMainWindowViewModel();
        var fullScreen = ShortcutRegistry.All.Single(s => s.Id == "Window.ToggleFullScreen");
        var requested = 0;
        mainWindowViewModel.ToggleFullScreenRequested += (_, _) => requested++;

        fullScreen.Command(mainWindowViewModel).Execute(null);

        await Assert.That(fullScreen.DefaultGesture).IsEqualTo("F11");
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
    [Test]
    [Arguments("Playback.VolumeUp", "Ctrl+Up")]
    [Arguments("Playback.VolumeDown", "Ctrl+Down")]
    [Arguments("Playback.ToggleMute", "Ctrl+M")]
    [Arguments("Playback.ToggleRepeat", "Ctrl+R")]
    [Arguments("Playback.ToggleAutoDj", "Ctrl+D")]
    [Arguments("Library.RevealPlayingTrack", "Ctrl+J")]
    public async Task NewShortcuts_HaveTheirDefaultGestures(string id, string gesture)
    {
        await Assert.That(ShortcutRegistry.All.Single(s => s.Id == id).DefaultGesture).IsEqualTo(gesture);
    }

    [Test]
    public async Task NewShortcuts_RunTheMatchingCommands()
    {
        var vm = CreateMainWindowViewModel();
        ICommand CommandOf(string id) => ShortcutRegistry.All.Single(s => s.Id == id).Command(vm);

        await Assert.That(CommandOf("Playback.VolumeUp")).IsSameReferenceAs(vm.PlaybackControls.Volume.VolumeUpCommand);
        await Assert.That(CommandOf("Playback.VolumeDown")).IsSameReferenceAs(vm.PlaybackControls.Volume.VolumeDownCommand);
        await Assert.That(CommandOf("Playback.ToggleMute")).IsSameReferenceAs(vm.PlaybackControls.Volume.ToggleMuteCommand);
        await Assert.That(CommandOf("Playback.ToggleRepeat")).IsSameReferenceAs(vm.PlaybackControls.ToggleRepeatModeCommand);
        await Assert.That(CommandOf("Playback.ToggleAutoDj")).IsSameReferenceAs(vm.PlaybackControls.ToggleAutoDjCommand);
        await Assert.That(CommandOf("Library.RevealPlayingTrack")).IsSameReferenceAs(vm.Library.RevealPlayingTrackCommand);
    }
}
