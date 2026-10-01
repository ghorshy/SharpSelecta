using System;
using Avalonia.Controls;

namespace SharpSelecta.App.Styles;

// The layout sizes the view models need as numbers (album tile zoom range, the grid's tile gap, the
// right column's initial width). They live in the theme like every other size - this just reads them
// once at startup and hands them down, so a custom theme overriding a token needs a restart to apply.
public sealed record ThemeLayout(
    double TileSizeDefault,
    double TileSizeMin,
    double TileSizeMax,
    double TileSizeStep,
    double TileSpacing,
    double RightColumnWidth)
{
    public static ThemeLayout From(IResourceHost host) => new(
        Number(host, "Size.Album.TileDefault"),
        Number(host, "Size.Album.TileMin"),
        Number(host, "Size.Album.TileMax"),
        Number(host, "Size.Album.TileStep"),
        // The same token the tiles' XAML spacing uses, so the column count always matches what's drawn.
        Number(host, SpacingScale.KeyFor(Spacing.L)),
        Number(host, "Size.MainWindow.RightColumnWidth"));

    private static double Number(IResourceHost host, string key) =>
        host.TryGetResource(key, null, out var value) && value is double number
            ? number
            : throw new InvalidOperationException($"Theme token '{key}' is missing or is not a number.");
}
