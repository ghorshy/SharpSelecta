using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class InterfaceSettingsViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    private readonly SettingsStore _settings;
    private readonly string _themesDirectory;
    private readonly IFilePickerService _filePickerService;

    // Maps a display name shown in the ComboBox back to the underlying custom theme's file name.
    private readonly Dictionary<string, string> _customThemeFileNamesByDisplayName = new();

    public ObservableCollection<string> ThemeDisplayNames { get; } = [];

    /// <summary>Where custom theme .axaml files are read from - shown so users can drop files in manually too.</summary>
    public string ThemesDirectory => _themesDirectory;

    public string CustomThemesFolderCaption => Strings.CustomThemesFolder(_themesDirectory);

    // Field-based, not a partial property: the constructor sets this before RemoveCustomThemeCommand
    // exists, and a partial property has no way to skip OnSelectedThemeDisplayNameChanged for that write.
    [ObservableProperty]
    private string _selectedThemeDisplayName;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    // A null size is the theme's own.
    public IReadOnlyList<FontSizeOption> FontSizeOptions { get; } =
    [
        new(null, Strings.FontSizeThemeDefault),
        .. AppearanceService.FontSizes.Select(size => new FontSizeOption(size, Strings.Pixels(size))),
    ];

    [ObservableProperty]
    public partial FontSizeOption SelectedFontSizeOption { get; set; }

    // What the color picker shows: the picked accent, or the one in effect when none is picked.
    [ObservableProperty]
    public partial Color AccentPickerColor { get; set; }

    [ObservableProperty]
    public partial string AccentHex { get; set; }

    [ObservableProperty]
    public partial bool IsAccentHexInvalid { get; set; }

    public bool HasCustomAccent => _settings.Current.AccentColor is not null;

    [ObservableProperty]
    public partial bool ShowTrackChangeNotifications { get; set; }

    // Only the freedesktop (Linux) notifications exist so far.
    public bool AreTrackChangeNotificationsSupported => OperatingSystem.IsLinux();

    [ObservableProperty]
    public partial bool MinimizeToTray { get; set; }

    [ObservableProperty]
    public partial bool CloseToTray { get; set; }

    public string DisplayName => Strings.SettingsCategoryInterface;

    public bool HasPendingChanges => false;

    public ICommand ApplyCommand { get; } = new RelayCommand(() => { });

    public ICommand CancelCommand { get; } = new RelayCommand(() => { });

    public IAsyncRelayCommand ImportThemeCommand { get; }

    public IRelayCommand RemoveCustomThemeCommand { get; }

    public ICommand RefreshThemesCommand { get; }

    public IRelayCommand<IBrush?> ChooseAccentSwatchCommand { get; }

    public IRelayCommand UseThemeAccentCommand { get; }

    // Set while the accent controls are brought in line with each other, so that doesn't count as a new pick.
    private bool _syncingAccent;

    // The constructor loads the saved values through the same properties; that isn't a change to save.
    private readonly bool _loaded;

    public InterfaceSettingsViewModel(SettingsStore settings, IFilePickerService filePickerService)
    {
        _settings = settings;
        _filePickerService = filePickerService;
        _themesDirectory = ThemeService.GetThemesDirectory(settings.FilePath);

        RefreshThemeDisplayNames();

        var theme = settings.Current.Theme;
        _selectedThemeDisplayName = theme == AppTheme.Custom
            ? DisplayNameForCustomFileOrFallback(settings.Current.CustomThemeFileName)
            : DisplayNameFor(theme);

        ImportThemeCommand = new AsyncRelayCommand(ImportThemeAsync);
        RemoveCustomThemeCommand = new RelayCommand(RemoveCustomTheme, () => IsCustomThemeSelected);
        RefreshThemesCommand = new RelayCommand(RefreshThemes);

        var saved = settings.Current;
        SelectedFontSizeOption = FontSizeOptions.FirstOrDefault(o => o.Size == saved.FontSize) ?? FontSizeOptions[0];
        ShowTrackChangeNotifications = saved.TrackChangeNotifications;
        MinimizeToTray = saved.MinimizeToTray;
        CloseToTray = saved.CloseToTray;
        AccentHex = saved.AccentColor ?? string.Empty;
        ChooseAccentSwatchCommand = new RelayCommand<IBrush?>(brush =>
        {
            if (brush is ISolidColorBrush solid)
                SetAccent(solid.Color);
        });
        UseThemeAccentCommand = new RelayCommand(() => SetAccent(null), () => HasCustomAccent);
        SyncAccentControls();
        _loaded = true;
    }

    partial void OnSelectedFontSizeOptionChanged(FontSizeOption value)
    {
        if (_loaded)
            UpdateAppearance(s => s with { FontSize = value.Size });
    }

    partial void OnShowTrackChangeNotificationsChanged(bool value)
    {
        if (_loaded)
            _settings.Update(s => s with { TrackChangeNotifications = value });
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        if (_loaded)
            _settings.Update(s => s with { MinimizeToTray = value });
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        if (_loaded)
            _settings.Update(s => s with { CloseToTray = value });
    }

    partial void OnAccentPickerColorChanged(Color value)
    {
        if (_loaded && !_syncingAccent)
            SetAccent(value);
    }

    // An empty field goes back to the theme's accent; a half-typed color waits until it's complete.
    partial void OnAccentHexChanged(string value)
    {
        if (!_loaded || _syncingAccent)
            return;

        var text = value.Trim();
        var color = AppearanceService.ParseAccent(text);
        IsAccentHexInvalid = color is null && text.Length > 0;
        if (color is not null || text.Length == 0)
            SetAccent(color);
    }

    private void SetAccent(Color? color)
    {
        var hex = color is { } picked ? AppearanceService.FormatAccent(picked) : null;
        if (hex == _settings.Current.AccentColor)
            return;

        UpdateAppearance(s => s with { AccentColor = hex });
        SyncAccentControls();
    }

    // The swatch, picker and hex field all edit the same color; whichever was used, the others follow.
    private void SyncAccentControls()
    {
        _syncingAccent = true;
        try
        {
            var picked = AppearanceService.ParseAccent(_settings.Current.AccentColor);
            AccentPickerColor = picked ?? AppearanceService.CurrentAccent() ?? AccentPickerColor;
            if (picked is not null || !IsAccentHexInvalid)
            {
                AccentHex = _settings.Current.AccentColor ?? string.Empty;
                IsAccentHexInvalid = false;
            }
        }
        finally
        {
            _syncingAccent = false;
        }

        OnPropertyChanged(nameof(HasCustomAccent));
        UseThemeAccentCommand.NotifyCanExecuteChanged();
    }

    private void UpdateAppearance(Func<AppSettings, AppSettings> change)
    {
        _settings.Update(change);
        AppearanceService.Apply(_settings.Current);
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
            _settings.Update(s => s with { Theme = AppTheme.Custom, CustomThemeFileName = fileName });
            ThemeService.Apply(AppTheme.Custom, fileName, _themesDirectory);
            ReapplyAppearance();
            return;
        }

        var theme = ThemeFor(value);
        _settings.Update(s => s with { Theme = theme });
        ThemeService.Apply(theme, null, _themesDirectory);
        ReapplyAppearance();
    }

    // The overrides are computed from the theme's values, and the accent picker shows the theme's accent.
    private void ReapplyAppearance()
    {
        AppearanceService.Apply(_settings.Current);
        SyncAccentControls();
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
        if (fileName is null) return Strings.ThemeDark;
        var displayName = Path.GetFileNameWithoutExtension(fileName);
        return _customThemeFileNamesByDisplayName.ContainsKey(displayName)
            ? displayName
            : Strings.ThemeDark;
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

public sealed record FontSizeOption(int? Size, string DisplayName);
