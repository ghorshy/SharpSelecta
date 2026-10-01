using SharpSelecta.App.Styles;

namespace SharpSelecta.Tests;

// Stand-in for the layout the app reads from its theme at startup (no Avalonia app in unit tests).
internal static class TestThemeLayout
{
    public static readonly ThemeLayout Default = new(
        TileSizeDefault: 160, TileSizeMin: 144, TileSizeMax: 320, TileSizeStep: 10, TileSpacing: 16, RightColumnWidth: 220);
}
