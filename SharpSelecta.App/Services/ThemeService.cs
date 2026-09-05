using Avalonia;
using Avalonia.Styling;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services;

internal static class ThemeService
{
    public static void Apply(AppTheme theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = ToThemeVariant(theme);
        }
    }

    private static ThemeVariant ToThemeVariant(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
