using System.Collections.Generic;
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

    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new("Library.FocusSearch", "Ctrl+F", () => Strings.ShortcutSearchLibrary, vm => vm.Library.FocusSearchCommand),
        new("Library.IncreaseTileSize", "Ctrl+OemPlus", () => Strings.ShortcutIncreaseTileSize,
            vm => new RelayCommand(() => ActiveGridOrNull(vm.Library)?.IncreaseTileSizeCommand.Execute(null))),
        new("Library.DecreaseTileSize", "Ctrl+OemMinus", () => Strings.ShortcutDecreaseTileSize,
            vm => new RelayCommand(() => ActiveGridOrNull(vm.Library)?.DecreaseTileSizeCommand.Execute(null))),
        new("Playback.SeekBackward", "Left", () => Strings.ShortcutSeekBackward, vm => vm.PlaybackControls.SeekBackwardCommand),
        new("Playback.SeekForward", "Right", () => Strings.ShortcutSeekForward, vm => vm.PlaybackControls.SeekForwardCommand),
        new("Playback.PlayPause", "MediaPlayPause", () => Strings.ShortcutPlayPause, vm => vm.PlaybackControls.PlayPauseCommand),
        new("Playback.PreviousTrack", "MediaPreviousTrack", () => Strings.ShortcutPreviousTrack, vm => vm.PlaybackControls.PreviousTrackCommand),
        new("Playback.NextTrack", "MediaNextTrack", () => Strings.ShortcutNextTrack, vm => vm.PlaybackControls.NextTrackCommand),
        new("Queue.ClearQueue", "", () => Strings.ShortcutClearQueue, vm => vm.Queue.ClearQueueCommand),
    ];
}
