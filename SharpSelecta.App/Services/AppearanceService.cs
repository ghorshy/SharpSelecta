using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services;

// The Interface settings laid over the theme: text size and accent color. Each override is
// computed from the theme's own value, so it works on top of a custom theme file just as on the built-in one.
// Re-apply after a theme change.
public static class AppearanceService
{
    public static IReadOnlyList<int> FontSizes { get; } = [12, 13, 14, 15, 16, 18, 20, 22, 24];

    private const string ContentFontSizeKey = "FontSize.Content";

    // Scaled by the same factor as the content text: the rest of the text, and the icons that sit next to it.
    private static readonly string[] ScaledKeys =
    [
        "FontSize.Small", "ControlContentThemeFontSize", "ToolTipContentThemeFontSize",
        "Size.Icon.XS", "Size.Icon.S", "Size.Icon.M",
    ];

    private static readonly string[] AccentKeys =
    [
        "SystemAccentColor",
        "SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorLight3",
        "SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3",
    ];

    private static ResourceDictionary? _applied;

    public static void Apply(AppSettings settings)
    {
        if (Application.Current is not { } app)
            return;

        var merged = app.Resources.MergedDictionaries;
        if (_applied is not null)
            merged.Remove(_applied);

        var overrides = new ResourceDictionary();
        foreach (var (key, value) in Compute(settings, key => app.TryGetResource(key, app.ActualThemeVariant, out var found) ? found : null))
        {
            overrides[key] = value;
        }

        // Merged dictionaries are searched last-first, so this one goes on top of the theme files.
        merged.Add(overrides);
        _applied = overrides;
    }

    // The accent in effect right now, whether picked, from the theme or from the system.
    public static Color? CurrentAccent() =>
        Application.Current is { } app && app.TryGetResource("SystemAccentColor", app.ActualThemeVariant, out var value) && value is Color color
            ? color
            : null;

    public static IReadOnlyDictionary<string, object> Compute(AppSettings settings, Func<string, object?> themeValue)
    {
        var overrides = new Dictionary<string, object>();

        if (settings.FontSize is { } fontSize && themeValue(ContentFontSizeKey) is double themeFontSize && fontSize != themeFontSize)
        {
            overrides[ContentFontSizeKey] = (double)fontSize;
            var factor = fontSize / themeFontSize;
            foreach (var key in ScaledKeys)
            {
                if (themeValue(key) is double size)
                    overrides[key] = Math.Round(size * factor, MidpointRounding.AwayFromZero);
            }
        }

        if (ParseAccent(settings.AccentColor) is { } accent)
        {
            // Fluent's own palette derives the lighter and darker shades its controls use.
            var palette = new ColorPaletteResources { Accent = accent };
            foreach (var key in AccentKeys)
            {
                if (palette.TryGetResource(key, null, out var shade) && shade is not null)
                    overrides[key] = shade;
            }
        }

        return overrides;
    }

    public static Color? ParseAccent(string? hex) =>
        hex is { Length: 7 } && hex[0] == '#' && Color.TryParse(hex, out var color) ? color : null;

    public static string FormatAccent(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
