using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services;

/// <summary>Applies the app's theme, including custom ResourceDictionary .axaml files from the Themes directory.</summary>
internal static class ThemeService
{
    private const string ThemesFolderName = "Themes";

    private static IResourceProvider? _appliedCustomTheme;

    // AvaloniaRuntimeXamlLoader.Load isn't safe to call concurrently from multiple threads.
    private static readonly object XamlLoadLock = new();

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

    /// <summary>Whether the file parses as an Avalonia ResourceDictionary, i.e. is usable as a custom theme.</summary>
    public static bool IsValidThemeFile(string filePath) => TryLoadThemeFile(filePath, out _);

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
        if (Application.Current is { } app && TryLoadThemeFile(filePath, out var provider))
        {
            app.Resources.MergedDictionaries.Add(provider!);
            _appliedCustomTheme = provider;
        }
    }

    private static void RemoveAppliedCustomTheme()
    {
        if (_appliedCustomTheme is not { } provider)
            return;

        if (Application.Current is { } app)
        {
            app.Resources.MergedDictionaries.Remove(provider);
        }

        _appliedCustomTheme = null;
    }

    // Custom theme files are arbitrary user input - a malformed one must never crash the app.
    private static bool TryLoadThemeFile(string filePath, out IResourceProvider? provider)
    {
        provider = null;
        try
        {
            if (!File.Exists(filePath))
                return false;

            var xaml = File.ReadAllText(filePath);
            lock (XamlLoadLock)
            {
                provider = AvaloniaRuntimeXamlLoader.Load(xaml) as IResourceProvider;
            }

            return provider is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static ThemeVariant ToThemeVariant(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
