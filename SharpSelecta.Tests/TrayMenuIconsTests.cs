using SharpSelecta.App.Services.Tray;
using SkiaSharp;

namespace SharpSelecta.Tests;

public class TrayMenuIconsTests
{
    private static string IconSvg(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SharpSelecta.App", "Assets", "Icons", fileName));

    [Test]
    [Arguments("player-play.svg")]
    [Arguments("player-pause.svg")]
    [Arguments("player-track-prev.svg")]
    [Arguments("player-track-next.svg")]
    [Arguments("app-window.svg")]
    [Arguments("power.svg")]
    public async Task RenderPng_DrawsTheIconAt32PixelsInTheGivenColor(string fileName)
    {
        var color = new Avalonia.Media.Color(0xFF, 0xE0, 0x40, 0x20);

        var png = TrayMenuIcons.RenderPng(IconSvg(fileName), color);

        using var bitmap = SKBitmap.Decode(png);
        await Assert.That(bitmap.Width).IsEqualTo(32);
        await Assert.That(bitmap.Height).IsEqualTo(32);
        var drawn = Enumerable.Range(0, 32 * 32).Select(i => bitmap.GetPixel(i % 32, i / 32)).Where(p => p.Alpha == 255).ToList();
        await Assert.That(drawn).IsNotEmpty();
        await Assert.That(drawn.All(p => p.Red == 0xE0 && p.Green == 0x40 && p.Blue == 0x20)).IsTrue();
    }
}
