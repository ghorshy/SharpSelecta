using Avalonia;

namespace SharpSelecta.App.Converters;

internal static class IconPaths
{
    public static string Resolve(string resourceKey, string fallbackPath) =>
        Application.Current is { } app &&
        app.TryGetResource(resourceKey, app.ActualThemeVariant, out var value) &&
        value is string path
            ? path
            : fallbackPath;
}
