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

    // Listed section by section, in the order the settings page shows them.
    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new("Playback.PlayPause", "MediaPlayPause", () => Strings.ShortcutPlayPause, vm => vm.PlaybackControls.PlayPauseCommand, ShortcutSection.Playback),
        new("Playback.PreviousTrack", "MediaPreviousTrack", () => Strings.ShortcutPreviousTrack, vm => vm.PlaybackControls.PreviousTrackCommand, ShortcutSection.Playback),
        new("Playback.NextTrack", "MediaNextTrack", () => Strings.ShortcutNextTrack, vm => vm.PlaybackControls.NextTrackCommand, ShortcutSection.Playback),
        new("Playback.SeekBackward", "Left", () => Strings.ShortcutSeekBackward, vm => vm.PlaybackControls.SeekBackwardCommand, ShortcutSection.Playback),
        new("Playback.SeekForward", "Right", () => Strings.ShortcutSeekForward, vm => vm.PlaybackControls.SeekForwardCommand, ShortcutSection.Playback),
        new("Playback.VolumeUp", "Ctrl+Up", () => Strings.ShortcutVolumeUp, vm => vm.PlaybackControls.Volume.VolumeUpCommand, ShortcutSection.Playback),
        new("Playback.VolumeDown", "Ctrl+Down", () => Strings.ShortcutVolumeDown, vm => vm.PlaybackControls.Volume.VolumeDownCommand, ShortcutSection.Playback),
        new("Playback.ToggleMute", "Ctrl+M", () => Strings.ShortcutToggleMute, vm => vm.PlaybackControls.Volume.ToggleMuteCommand, ShortcutSection.Playback),
        new("Playback.ToggleRepeat", "Ctrl+R", () => Strings.ShortcutToggleRepeat, vm => vm.PlaybackControls.ToggleRepeatModeCommand, ShortcutSection.Playback),
        new("Playback.ToggleAutoDj", "Ctrl+D", () => Strings.ShortcutToggleAutoDj, vm => vm.PlaybackControls.ToggleAutoDjCommand, ShortcutSection.Playback),
        new("Library.Refresh", "F5", () => Strings.ShortcutRefreshLibrary, vm => vm.Library.RescanCommand, ShortcutSection.Library),
        new("Library.FocusSearch", "Ctrl+F", () => Strings.ShortcutSearchLibrary, vm => vm.Library.Search.FocusCommand, ShortcutSection.Library),
        new("Library.RevealPlayingTrack", "Ctrl+J", () => Strings.ShortcutRevealPlayingTrack, vm => vm.Library.RevealPlayingTrackCommand, ShortcutSection.Library),
        new("Library.Properties", "Alt+Enter", () => Strings.ShortcutProperties, vm => vm.OpenPropertiesCommand, ShortcutSection.Library,
            ShortcutScope.TrackList | ShortcutScope.AlbumGrid | ShortcutScope.Queue),
        new("Library.IncreaseTileSize", "Ctrl+OemPlus", () => Strings.ShortcutIncreaseTileSize, IncreaseTileSize, ShortcutSection.AlbumGrid, ShortcutScope.AlbumGrid),
        new("Library.DecreaseTileSize", "Ctrl+OemMinus", () => Strings.ShortcutDecreaseTileSize, DecreaseTileSize, ShortcutSection.AlbumGrid, ShortcutScope.AlbumGrid),
        new("Library.IncreaseTileSizeWheel", "Ctrl+WheelUp", () => Strings.ShortcutIncreaseTileSize, IncreaseTileSize, ShortcutSection.AlbumGrid, ShortcutScope.AlbumGrid),
        new("Library.DecreaseTileSizeWheel", "Ctrl+WheelDown", () => Strings.ShortcutDecreaseTileSize, DecreaseTileSize, ShortcutSection.AlbumGrid, ShortcutScope.AlbumGrid),
        new("Queue.ClearQueue", "", () => Strings.ShortcutClearQueue, vm => vm.Queue.ClearQueueCommand, ShortcutSection.Queue, ShortcutScope.Queue),
        new("Window.ToggleFullScreen", "F11", () => Strings.ShortcutToggleFullScreen, vm => vm.ToggleFullScreenCommand, ShortcutSection.Window),
    ];
}
