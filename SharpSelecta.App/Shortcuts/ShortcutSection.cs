using SharpSelecta.App.Resources;

namespace SharpSelecta.App.Shortcuts;

// HomeScope is the view the section is named after; rows scoped to just that view need no scope label.
public sealed record ShortcutSection(Func<string> Title, ShortcutScope HomeScope = ShortcutScope.Global)
{
    public static ShortcutSection Playback { get; } = new(() => Strings.ShortcutSectionPlayback);
    public static ShortcutSection Library { get; } = new(() => Strings.ShortcutSectionLibrary);
    public static ShortcutSection AlbumGrid { get; } = new(() => Strings.ShortcutSectionAlbumGrid, ShortcutScope.AlbumGrid);
    public static ShortcutSection Queue { get; } = new(() => Strings.ShortcutSectionQueue, ShortcutScope.Queue);
    public static ShortcutSection Window { get; } = new(() => Strings.ShortcutSectionWindow);
}
