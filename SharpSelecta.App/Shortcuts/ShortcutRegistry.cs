using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Shortcuts;

public static class ShortcutRegistry
{
    // Re-resolved per keypress, not once at command construction, so it tracks whichever
    // grid is active when the shortcut fires instead of freezing on the section at startup.
    private static AlbumGridViewModel? ActiveGridOrNull(LibraryViewModel library) => library.LibrarySection switch
    {
        LibrarySection.Library => library.Grid,
        LibrarySection.RecentlyAdded => library.RecentlyAddedGrid,
        _ => null, // Playlist has no Cover Art view - nothing to resize
    };

    private static RelayCommand<double?> IncreaseTileSize(MainWindowViewModel vm) =>
        new(steps => ActiveGridOrNull(vm.Library)?.IncreaseTileSizeCommand.Execute(steps));

    private static RelayCommand<double?> DecreaseTileSize(MainWindowViewModel vm) =>
        new(steps => ActiveGridOrNull(vm.Library)?.DecreaseTileSizeCommand.Execute(steps));

    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new("Library.Refresh", "F5", () => Strings.ShortcutRefreshLibrary, vm => vm.Library.RescanCommand),
        new("Library.FocusSearch", "Ctrl+F", () => Strings.ShortcutSearchLibrary, vm => vm.Library.Search.FocusCommand),
        new("Library.IncreaseTileSize", "Ctrl+OemPlus", () => Strings.ShortcutIncreaseTileSize, IncreaseTileSize, ShortcutScope.AlbumGrid),
        new("Library.DecreaseTileSize", "Ctrl+OemMinus", () => Strings.ShortcutDecreaseTileSize, DecreaseTileSize, ShortcutScope.AlbumGrid),
        new("Library.IncreaseTileSizeWheel", "Ctrl+WheelUp", () => Strings.ShortcutIncreaseTileSizeWheel, IncreaseTileSize, ShortcutScope.AlbumGrid),
        new("Library.DecreaseTileSizeWheel", "Ctrl+WheelDown", () => Strings.ShortcutDecreaseTileSizeWheel, DecreaseTileSize, ShortcutScope.AlbumGrid),
        new("Playback.SeekBackward", "Left", () => Strings.ShortcutSeekBackward, vm => vm.PlaybackControls.SeekBackwardCommand),
        new("Playback.SeekForward", "Right", () => Strings.ShortcutSeekForward, vm => vm.PlaybackControls.SeekForwardCommand),
        new("Playback.PlayPause", "MediaPlayPause", () => Strings.ShortcutPlayPause, vm => vm.PlaybackControls.PlayPauseCommand),
        new("Playback.PreviousTrack", "MediaPreviousTrack", () => Strings.ShortcutPreviousTrack, vm => vm.PlaybackControls.PreviousTrackCommand),
        new("Playback.NextTrack", "MediaNextTrack", () => Strings.ShortcutNextTrack, vm => vm.PlaybackControls.NextTrackCommand),
        new("Queue.ClearQueue", "", () => Strings.ShortcutClearQueue, vm => vm.Queue.ClearQueueCommand, ShortcutScope.Queue),
        new("Library.Properties", "Alt+Enter", () => Strings.ShortcutProperties, vm => vm.OpenPropertiesCommand,
            ShortcutScope.TrackList | ShortcutScope.AlbumGrid | ShortcutScope.Queue),
    ];
}
