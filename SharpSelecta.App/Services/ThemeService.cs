using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services;

/// <summary>Applies the app's theme, including custom ResourceDictionary .axaml files from the Themes directory.</summary>
internal static class ThemeService
{
    private const string ThemesFolderName = "Themes";

    private static ParsedTheme? _appliedCustomTheme;

    public static string GetThemesDirectory(string settingsFilePath) =>
        Path.Combine(Path.GetDirectoryName(settingsFilePath) ?? ".", ThemesFolderName);

    public static IReadOnlyList<string> ListCustomThemeFileNames(string themesDirectory) =>
        Directory.Exists(themesDirectory)
            ? Directory.GetFiles(themesDirectory, "*.axaml")
                .Select(Path.GetFileName)
                .OfType<string>()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

    // Null when the file is usable as a custom theme, otherwise what's wrong with it.
    public static string? ValidateThemeFile(string filePath)
    {
        var error = TryLoadThemeFile(filePath, out var theme);
        theme?.Dispose();
        return error;
    }

    public static void Apply(AppTheme theme, string? customThemeFileName, string themesDirectory)
    {
        RemoveAppliedCustomTheme();

        if (theme == AppTheme.Custom && customThemeFileName is { } fileName)
        {
            ApplyCustomTheme(Path.Combine(themesDirectory, fileName));
            return;
        }

        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = ToThemeVariant(theme);
        }
    }

    private static void ApplyCustomTheme(string filePath)
    {
        if (Application.Current is { } app && TryLoadThemeFile(filePath, out var theme) is null)
        {
            app.Resources.MergedDictionaries.Add(theme!.Resources);
            _appliedCustomTheme = theme;
        }
    }

    private static void RemoveAppliedCustomTheme()
    {
        if (_appliedCustomTheme is not { } theme)
            return;

        if (Application.Current is { } app)
        {
            app.Resources.MergedDictionaries.Remove(theme.Resources);
        }

        theme.Dispose();
        _appliedCustomTheme = null;
    }

    // Custom theme files are arbitrary user input - a malformed one must never crash the app.
    private static string? TryLoadThemeFile(string filePath, out ParsedTheme? theme)
    {
        theme = null;
        try
        {
            var app = Application.Current;
            theme = ThemeFileParser.Parse(File.ReadAllText(filePath), app, app is null ? null : key => ExistingType(app, key));
            return null;
        }
        catch (ThemeFileException exception)
        {
            return Strings.InvalidThemeFileAt(exception.Line, exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Strings.InvalidThemeFile;
        }
    }

    // The type a key has among the app's resources (our tokens, Fluent's), in either theme variant.
    private static Type? ExistingType(Application app, string key) =>
        (app.TryGetResource(key, ThemeVariant.Dark, out var dark) ? dark
            : app.TryGetResource(key, ThemeVariant.Light, out var light) ? light
            : null)?.GetType();

    private static ThemeVariant ToThemeVariant(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
