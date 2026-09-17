using System.Collections.Generic;
using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Shortcuts;

public static class ShortcutRegistry
{
    private static AlbumGridViewModel ActiveGrid(LibraryViewModel library) =>
        library.LibrarySection == LibrarySection.RecentlyAdded ? library.RecentlyAddedGrid : library.Grid;

    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new("Library.FocusSearch", "Ctrl+F", () => Strings.ShortcutSearchLibrary, vm => vm.Library.FocusSearchCommand),
        new("Library.IncreaseTileSize", "Ctrl+OemPlus", () => Strings.ShortcutIncreaseTileSize, vm => ActiveGrid(vm.Library).IncreaseTileSizeCommand),
        new("Library.DecreaseTileSize", "Ctrl+OemMinus", () => Strings.ShortcutDecreaseTileSize, vm => ActiveGrid(vm.Library).DecreaseTileSizeCommand),
        new("Playback.SeekBackward", "Left", () => Strings.ShortcutSeekBackward, vm => vm.PlaybackControls.SeekBackwardCommand),
        new("Playback.SeekForward", "Right", () => Strings.ShortcutSeekForward, vm => vm.PlaybackControls.SeekForwardCommand),
        new("Playback.PlayPause", "MediaPlayPause", () => Strings.ShortcutPlayPause, vm => vm.PlaybackControls.PlayPauseCommand),
        new("Playback.PreviousTrack", "MediaPreviousTrack", () => Strings.ShortcutPreviousTrack, vm => vm.PlaybackControls.PreviousTrackCommand),
        new("Playback.NextTrack", "MediaNextTrack", () => Strings.ShortcutNextTrack, vm => vm.PlaybackControls.NextTrackCommand),
        new("Queue.ClearQueue", "", () => Strings.ShortcutClearQueue, vm => vm.Queue.ClearQueueCommand),
    ];
}
