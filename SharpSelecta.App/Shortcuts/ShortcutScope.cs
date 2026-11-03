namespace SharpSelecta.App.Shortcuts;

[Flags]
public enum ShortcutScope
{
    Global = 0,
    AlbumGrid = 1,
    TrackList = 2,
    Queue = 4,
}

public static class ShortcutScopeExtensions
{
    public static bool IsActiveIn(this ShortcutScope scope, ShortcutScope activeScopes) =>
        scope == ShortcutScope.Global || (scope & activeScopes) != 0;

    public static bool Overlaps(this ShortcutScope scope, ShortcutScope other) =>
        scope == ShortcutScope.Global || other == ShortcutScope.Global || (scope & other) != 0;
}
