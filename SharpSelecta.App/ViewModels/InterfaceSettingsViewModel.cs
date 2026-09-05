using System.Collections.Generic;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class InterfaceSettingsViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    private readonly string _settingsFilePath;

    public IReadOnlyList<string> ThemeDisplayNames { get; } = [Strings.ThemeSystem, Strings.ThemeLight, Strings.ThemeDark];

    [ObservableProperty]
    private string selectedThemeDisplayName;

    public bool HasPendingChanges => false;

    public ICommand ApplyCommand { get; } = new RelayCommand(() => { });

    public ICommand CancelCommand { get; } = new RelayCommand(() => { });

    public InterfaceSettingsViewModel(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
        selectedThemeDisplayName = DisplayNameFor(SettingsStore.LoadTheme(settingsFilePath));
    }

    partial void OnSelectedThemeDisplayNameChanged(string value)
    {
        var theme = ThemeFor(value);
        SettingsStore.SaveTheme(_settingsFilePath, theme);
        ThemeService.Apply(theme);
    }

    private static string DisplayNameFor(AppTheme theme) => theme switch
    {
        AppTheme.Light => Strings.ThemeLight,
        AppTheme.Dark => Strings.ThemeDark,
        _ => Strings.ThemeSystem,
    };

    private static AppTheme ThemeFor(string displayName) => displayName switch
    {
        _ when displayName == Strings.ThemeLight => AppTheme.Light,
        _ when displayName == Strings.ThemeDark => AppTheme.Dark,
        _ => AppTheme.System,
    };
}
