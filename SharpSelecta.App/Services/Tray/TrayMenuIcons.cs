using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using Svg.Skia;

namespace SharpSelecta.App.Services.Tray;

// The tray menu takes bitmaps, not the SVG controls the rest of the app draws icons with, so the same
// icon files are rendered once here in the menu's icon color.
public static class TrayMenuIcons
{
    private const int PixelSize = 32;

    // Linux panels draw the menu in their own style; on Windows it follows the app's theme.
    public static Color? IconColor() =>
        Resource(OperatingSystem.IsLinux() ? "Color.TrayMenuIcon" : "Color.IconForeground") as Color?;

    public static Bitmap? Render(string iconKey, Color color)
    {
        if (Resource(iconKey) is not string uri)
            return null;

        try
        {
            using var reader = new StreamReader(AssetLoader.Open(new Uri(uri)));
            return RenderPng(reader.ReadToEnd(), color) is { } png ? new Bitmap(new MemoryStream(png)) : null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    // The icon files draw in currentColor.
    public static byte[]? RenderPng(string svgText, Color color)
    {
        using var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgText.Replace("currentColor", $"#{color.R:X2}{color.G:X2}{color.B:X2}")));
        if (svg.Load(input) is not { } picture || picture.CullRect.Width <= 0)
            return null;

        var scale = PixelSize / picture.CullRect.Width;
        using var output = new MemoryStream();
        return svg.Save(output, SKColors.Transparent, SKEncodedImageFormat.Png, 100, scale, scale) ? output.ToArray() : null;
    }

    private static object? Resource(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var value) ? value : null;
}
