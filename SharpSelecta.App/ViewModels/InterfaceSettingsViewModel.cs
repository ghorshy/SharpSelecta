using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// Like the other categories, edits are saved only on Apply. The look (theme, accent, text size) and the
// side panel's layout are previewed live while editing, and Cancel puts the applied ones back.
public partial class InterfaceSettingsViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    private sealed record Choices(
        AppTheme Theme, string? CustomThemeFileName, int? FontSize, string? AccentColor,
        bool TrackChangeNotifications, bool MinimizeToTray, bool CloseToTray,
        SidePanelPosition SidePanelPosition, QueuePosition QueuePosition, bool ShowNowPlayingArtwork, bool ShowNowPlayingDetails,
        PlaybackBarLayout PlaybackBar, bool UseWaveformSlider)
    {
        public static Choices From(AppSettings s) =>
            new(s.Theme, s.CustomThemeFileName, s.FontSize, s.AccentColor, s.TrackChangeNotifications, s.MinimizeToTray, s.CloseToTray,
                s.SidePanelPosition, s.QueuePosition, s.ShowNowPlayingArtwork, s.ShowNowPlayingDetails, s.PlaybackBar, s.UseWaveformSlider);

        public AppSettings ApplyTo(AppSettings s) => s with
        {
            Theme = Theme, CustomThemeFileName = CustomThemeFileName, FontSize = FontSize, AccentColor = AccentColor,
            TrackChangeNotifications = TrackChangeNotifications, MinimizeToTray = MinimizeToTray, CloseToTray = CloseToTray,
            SidePanelPosition = SidePanelPosition, QueuePosition = QueuePosition,
            ShowNowPlayingArtwork = ShowNowPlayingArtwork, ShowNowPlayingDetails = ShowNowPlayingDetails,
            PlaybackBar = PlaybackBar, UseWaveformSlider = UseWaveformSlider,
        };
    }

    private readonly SettingsStore _settings;
    private Choices _applied;
    private Choices _edited;
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

    public bool HasCustomAccent => _edited.AccentColor is not null;

    [ObservableProperty]
    public partial bool ShowTrackChangeNotifications { get; set; }

    // Only the freedesktop (Linux) notifications exist so far.
    public bool AreTrackChangeNotificationsSupported => OperatingSystem.IsLinux();

    [ObservableProperty]
    public partial bool MinimizeToTray { get; set; }

    [ObservableProperty]
    public partial bool CloseToTray { get; set; }

    public IReadOnlyList<SidePanelPositionOption> SidePanelPositionOptions { get; } =
    [
        new(SidePanelPosition.Right, Strings.SidePanelRight),
        new(SidePanelPosition.Left, Strings.SidePanelLeft),
        new(SidePanelPosition.Hidden, Strings.SidePanelHidden),
    ];

    // The side panel's controls show the edited layout, which the main window previews.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSidePanelShown))]
    public partial SidePanelPositionOption SelectedSidePanelPositionOption { get; set; }

    public bool IsSidePanelShown => SelectedSidePanelPositionOption.Position != SidePanelPosition.Hidden;

    public IReadOnlyList<QueuePositionOption> QueuePositionOptions { get; } =
    [
        new(QueuePosition.Top, Strings.QueueOnTop),
        new(QueuePosition.Bottom, Strings.QueueAtTheBottom),
    ];

    [ObservableProperty]
    public partial QueuePositionOption SelectedQueuePositionOption { get; set; }

    [ObservableProperty]
    public partial bool ShowNowPlayingArtwork { get; set; }

    [ObservableProperty]
    public partial bool ShowNowPlayingDetails { get; set; }

    // The bottom bar as edited; the main window previews it.
    public PlaybackBarLayout PlaybackBar => _edited.PlaybackBar;

    public PlaybackBarEditorViewModel PlaybackBarEditor { get; }

    private bool _revealPlaybackBar;

    // One-shot: the page scrolls to the bar's section the next time it's shown.
    public void RequestPlaybackBarReveal() => _revealPlaybackBar = true;

    public bool TakePlaybackBarReveal()
    {
        var reveal = _revealPlaybackBar;
        _revealPlaybackBar = false;
        return reveal;
    }

    // Previewed on the bar like its layout.
    [ObservableProperty]
    public partial bool UseWaveformSlider { get; set; }

    public string DisplayName => Strings.SettingsCategoryInterface;

    public bool HasPendingChanges => _edited != _applied;

    public ICommand ApplyCommand { get; }

    public ICommand CancelCommand { get; }

    public event EventHandler? Applied;

    public IAsyncRelayCommand ImportThemeCommand { get; }

    public IRelayCommand RemoveCustomThemeCommand { get; }

    public ICommand RefreshThemesCommand { get; }

    public IRelayCommand<IBrush?> ChooseAccentSwatchCommand { get; }

    public IRelayCommand UseThemeAccentCommand { get; }

    // Set while the accent controls are brought in line with each other, so that doesn't count as a new pick.
    private bool _syncingAccent;

    // Set while the controls are loaded from a snapshot (constructor, Cancel); that isn't an edit.
    private bool _loading = true;

    public InterfaceSettingsViewModel(SettingsStore settings, IFilePickerService filePickerService)
    {
        _settings = settings;
        _filePickerService = filePickerService;
        _applied = Choices.From(settings.Current);
        _edited = _applied;
        _themesDirectory = ThemeService.GetThemesDirectory(settings.FilePath);

        RefreshThemeDisplayNames();

        var theme = settings.Current.Theme;
        _selectedThemeDisplayName = theme == AppTheme.Custom
            ? DisplayNameForCustomFileOrFallback(settings.Current.CustomThemeFileName)
            : DisplayNameFor(theme);

        ImportThemeCommand = new AsyncRelayCommand(ImportThemeAsync);
        RemoveCustomThemeCommand = new RelayCommand(RemoveCustomTheme, () => IsCustomThemeSelected);
        RefreshThemesCommand = new RelayCommand(RefreshThemes);

        AccentHex = string.Empty;
        ChooseAccentSwatchCommand = new RelayCommand<IBrush?>(brush =>
        {
            if (brush is ISolidColorBrush solid)
                SetAccent(solid.Color);
        });
        UseThemeAccentCommand = new RelayCommand(() => SetAccent(null), () => HasCustomAccent);
        ApplyCommand = new RelayCommand(Apply);
        CancelCommand = new RelayCommand(Cancel);
        PlaybackBarEditor = new PlaybackBarEditorViewModel(_applied.PlaybackBar, layout => Edit(c => c with { PlaybackBar = layout }));
        ShowChoices(_applied);
        _loading = false;
    }

    private void Apply()
    {
        if (!HasPendingChanges)
            return;

        var edited = _edited;
        _settings.Update(edited.ApplyTo);
        _applied = edited;
        OnPropertyChanged(nameof(HasPendingChanges));
        Applied?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel()
    {
        var hadPreview = _edited.Theme != _applied.Theme || _edited.CustomThemeFileName != _applied.CustomThemeFileName
            || _edited.FontSize != _applied.FontSize || _edited.AccentColor != _applied.AccentColor;
        var barChanged = _edited.PlaybackBar != _applied.PlaybackBar;
        _edited = _applied;
        if (barChanged)
            OnPropertyChanged(nameof(PlaybackBar));
        ShowChoices(_applied);
        OnPropertyChanged(nameof(HasPendingChanges));
        if (hadPreview)
            Preview(themeChanged: true);
    }

    [MemberNotNull(nameof(SelectedFontSizeOption), nameof(SelectedSidePanelPositionOption), nameof(SelectedQueuePositionOption))]
    private void ShowChoices(Choices choices)
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            SelectedThemeDisplayName = choices.Theme == AppTheme.Custom
                ? DisplayNameForCustomFileOrFallback(choices.CustomThemeFileName)
                : DisplayNameFor(choices.Theme);
            SelectedFontSizeOption = FontSizeOptions.FirstOrDefault(o => o.Size == choices.FontSize) ?? FontSizeOptions[0];
            ShowTrackChangeNotifications = choices.TrackChangeNotifications;
            MinimizeToTray = choices.MinimizeToTray;
            CloseToTray = choices.CloseToTray;
            SelectedSidePanelPositionOption = SidePanelPositionOptions.First(o => o.Position == choices.SidePanelPosition);
            SelectedQueuePositionOption = QueuePositionOptions.First(o => o.Position == choices.QueuePosition);
            ShowNowPlayingArtwork = choices.ShowNowPlayingArtwork;
            ShowNowPlayingDetails = choices.ShowNowPlayingDetails;
            PlaybackBarEditor.Load(choices.PlaybackBar);
            UseWaveformSlider = choices.UseWaveformSlider;
            IsAccentHexInvalid = false;
            SyncAccentControls();
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    private void Edit(Func<Choices, Choices> change)
    {
        var bar = _edited.PlaybackBar;
        _edited = change(_edited);
        OnPropertyChanged(nameof(HasPendingChanges));
        if (_edited.PlaybackBar != bar)
            OnPropertyChanged(nameof(PlaybackBar));
    }

    // The overrides are computed from the theme's values, and the accent picker shows the theme's accent.
    private void Preview(bool themeChanged)
    {
        if (themeChanged)
            ThemeService.Apply(_edited.Theme, _edited.CustomThemeFileName, _themesDirectory);

        AppearanceService.Apply(_edited.ApplyTo(_settings.Current));
        SyncAccentControls();
    }

    partial void OnSelectedFontSizeOptionChanged(FontSizeOption value)
    {
        if (_loading)
            return;

        Edit(c => c with { FontSize = value.Size });
        Preview(themeChanged: false);
    }

    partial void OnShowTrackChangeNotificationsChanged(bool value)
    {
        if (!_loading)
            Edit(c => c with { TrackChangeNotifications = value });
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        if (!_loading)
            Edit(c => c with { MinimizeToTray = value });
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        if (!_loading)
            Edit(c => c with { CloseToTray = value });
    }

    partial void OnSelectedSidePanelPositionOptionChanged(SidePanelPositionOption value)
    {
        if (!_loading)
            Edit(c => c with { SidePanelPosition = value.Position });
    }

    partial void OnSelectedQueuePositionOptionChanged(QueuePositionOption value)
    {
        if (!_loading)
            Edit(c => c with { QueuePosition = value.Position });
    }

    partial void OnShowNowPlayingArtworkChanged(bool value)
    {
        if (!_loading)
            Edit(c => c with { ShowNowPlayingArtwork = value });
    }

    partial void OnShowNowPlayingDetailsChanged(bool value)
    {
        if (!_loading)
            Edit(c => c with { ShowNowPlayingDetails = value });
    }

    partial void OnUseWaveformSliderChanged(bool value)
    {
        if (!_loading)
            Edit(c => c with { UseWaveformSlider = value });
    }

    partial void OnAccentPickerColorChanged(Color value)
    {
        if (!_loading && !_syncingAccent)
            SetAccent(value);
    }

    // An empty field goes back to the theme's accent; a half-typed color waits until it's complete.
    partial void OnAccentHexChanged(string value)
    {
        if (_loading || _syncingAccent)
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
        if (hex == _edited.AccentColor)
            return;

        Edit(c => c with { AccentColor = hex });
        Preview(themeChanged: false);
    }

    // The swatch, picker and hex field all edit the same color; whichever was used, the others follow.
    private void SyncAccentControls()
    {
        _syncingAccent = true;
        try
        {
            var picked = AppearanceService.ParseAccent(_edited.AccentColor);
            AccentPickerColor = picked ?? AppearanceService.CurrentAccent() ?? AccentPickerColor;
            if (picked is not null || !IsAccentHexInvalid)
            {
                AccentHex = _edited.AccentColor ?? string.Empty;
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

    partial void OnSelectedThemeDisplayNameChanged(string value)
    {
        RemoveCustomThemeCommand.NotifyCanExecuteChanged();

        // Clearing ThemeDisplayNames mid-refresh transiently nulls the bound ComboBox selection.
        // The caller always sets a real value right after, so just ignore this.
        if (value is null || _loading)
            return;

        if (_customThemeFileNamesByDisplayName.TryGetValue(value, out var fileName))
            Edit(c => c with { Theme = AppTheme.Custom, CustomThemeFileName = fileName });
        else
            Edit(c => c with { Theme = ThemeFor(value) });

        Preview(themeChanged: true);
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

public sealed record SidePanelPositionOption(SidePanelPosition Position, string DisplayName);

public sealed record QueuePositionOption(QueuePosition Position, string DisplayName);
