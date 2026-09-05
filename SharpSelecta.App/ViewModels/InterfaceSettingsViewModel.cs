using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
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
    private readonly string _themesDirectory;
    private readonly IFilePickerService _filePickerService;

    // Maps a display name shown in the ComboBox back to the underlying custom theme's file name.
    private readonly Dictionary<string, string> _customThemeFileNamesByDisplayName = new();

    public ObservableCollection<string> ThemeDisplayNames { get; } = [];

    /// <summary>Where custom theme .axaml files are read from - shown so users can drop files in manually too.</summary>
    public string ThemesDirectory => _themesDirectory;

    public string CustomThemesFolderCaption => Strings.CustomThemesFolder(_themesDirectory);

    [ObservableProperty]
    private string selectedThemeDisplayName;

    [ObservableProperty]
    private string? statusMessage;

    public bool HasPendingChanges => false;

    public ICommand ApplyCommand { get; } = new RelayCommand(() => { });

    public ICommand CancelCommand { get; } = new RelayCommand(() => { });

    public IAsyncRelayCommand ImportThemeCommand { get; }

    public IRelayCommand RemoveCustomThemeCommand { get; }

    public ICommand RefreshThemesCommand { get; }

    public InterfaceSettingsViewModel(string settingsFilePath, IFilePickerService filePickerService)
    {
        _settingsFilePath = settingsFilePath;
        _filePickerService = filePickerService;
        _themesDirectory = ThemeService.GetThemesDirectory(settingsFilePath);

        RefreshThemeDisplayNames();

        var theme = SettingsStore.LoadTheme(settingsFilePath);
        selectedThemeDisplayName = theme == AppTheme.Custom
            ? DisplayNameForCustomFileOrFallback(SettingsStore.LoadCustomThemeFileName(settingsFilePath))
            : DisplayNameFor(theme);

        ImportThemeCommand = new AsyncRelayCommand(ImportThemeAsync);
        RemoveCustomThemeCommand = new RelayCommand(RemoveCustomTheme, () => IsCustomThemeSelected);
        RefreshThemesCommand = new RelayCommand(RefreshThemes);
    }

    partial void OnSelectedThemeDisplayNameChanged(string value)
    {
        RemoveCustomThemeCommand.NotifyCanExecuteChanged();

        // Clearing ThemeDisplayNames mid-refresh transiently nulls the bound ComboBox selection.
        // The caller always sets a real value right after, so just ignore this.
        if (value is null)
            return;

        if (_customThemeFileNamesByDisplayName.TryGetValue(value, out var fileName))
        {
            SettingsStore.SaveTheme(_settingsFilePath, AppTheme.Custom);
            SettingsStore.SaveCustomThemeFileName(_settingsFilePath, fileName);
            ThemeService.Apply(AppTheme.Custom, fileName, _themesDirectory);
            return;
        }

        var theme = ThemeFor(value);
        SettingsStore.SaveTheme(_settingsFilePath, theme);
        ThemeService.Apply(theme, null, _themesDirectory);
    }

    private async Task ImportThemeAsync()
    {
        var pickedPath = await _filePickerService.PickThemeFileAsync();
        if (pickedPath is null)
            return;

        StatusMessage = null;

        if (!ThemeService.IsValidThemeFile(pickedPath))
        {
            StatusMessage = Strings.InvalidThemeFile;
            return;
        }

        Directory.CreateDirectory(_themesDirectory);
        var destinationFileName = UniqueFileName(Path.GetFileName(pickedPath));
        var destinationPath = Path.Combine(_themesDirectory, destinationFileName);

        try
        {
            File.Copy(pickedPath, destinationPath, overwrite: false);
        }
        catch (IOException ex)
        {
            StatusMessage = Strings.FailedToImportTheme(ex.Message);
            return;
        }

        RefreshThemeDisplayNames();
        SelectedThemeDisplayName = Path.GetFileNameWithoutExtension(destinationFileName);
    }

    private void RemoveCustomTheme()
    {
        if (!_customThemeFileNamesByDisplayName.TryGetValue(SelectedThemeDisplayName, out var fileName))
            return;

        StatusMessage = null;

        try
        {
            File.Delete(Path.Combine(_themesDirectory, fileName));
        }
        catch (IOException ex)
        {
            StatusMessage = Strings.FailedToRemoveTheme(ex.Message);
        }

        RefreshThemeDisplayNames();
        SelectedThemeDisplayName = Strings.ThemeDark;
    }

    private bool IsCustomThemeSelected =>
        SelectedThemeDisplayName is { } selection && _customThemeFileNamesByDisplayName.ContainsKey(selection);

    private void RefreshThemes()
    {
        var previousSelection = SelectedThemeDisplayName;
        RefreshThemeDisplayNames();

        // Refresh nulls the selection (see OnSelectedThemeDisplayNameChanged), so always restore it here.
        SelectedThemeDisplayName = previousSelection is { } selection && ThemeDisplayNames.Contains(selection)
            ? selection
            : Strings.ThemeDark;
    }

    private void RefreshThemeDisplayNames()
    {
        _customThemeFileNamesByDisplayName.Clear();
        ThemeDisplayNames.Clear();
        ThemeDisplayNames.Add(Strings.ThemeSystem);
        ThemeDisplayNames.Add(Strings.ThemeLight);
        ThemeDisplayNames.Add(Strings.ThemeDark);

        foreach (var fileName in ThemeService.ListCustomThemeFileNames(_themesDirectory))
        {
            var displayName = UniqueDisplayName(Path.GetFileNameWithoutExtension(fileName));
            _customThemeFileNamesByDisplayName[displayName] = fileName;
            ThemeDisplayNames.Add(displayName);
        }
    }

    private string DisplayNameForCustomFileOrFallback(string? fileName)
    {
        if (fileName is { } name)
        {
            var displayName = Path.GetFileNameWithoutExtension(name);
            if (_customThemeFileNamesByDisplayName.ContainsKey(displayName))
                return displayName;
        }

        // The saved custom theme file is missing (e.g. deleted outside the app) - fall back to the app default.
        return Strings.ThemeDark;
    }

    private string UniqueDisplayName(string candidate)
    {
        var result = candidate;
        for (var suffix = 2; ThemeDisplayNames.Contains(result) || _customThemeFileNamesByDisplayName.ContainsKey(result); suffix++)
        {
            result = $"{candidate} ({suffix})";
        }

        return result;
    }

    private string UniqueFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = fileName;
        for (var suffix = 2; File.Exists(Path.Combine(_themesDirectory, candidate)); suffix++)
        {
            candidate = $"{name} ({suffix}){extension}";
        }

        return candidate;
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
