using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed record DiscordPresenceOptions(bool ShowArtist, bool ShowAlbum, bool ShowElapsed, bool ShowWhilePaused)
{
    public static DiscordPresenceOptions From(AppSettings settings) =>
        new(settings.DiscordShowArtist, settings.DiscordShowAlbum, settings.DiscordShowElapsed, settings.DiscordShowWhilePaused);
}

// Settings for talking to other programs. Like the other categories, edits only take effect on Apply.
public sealed partial class IntegrationsSettingsViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    private readonly SettingsStore _settings;
    private bool _appliedDiscordPresenceEnabled;
    private DiscordPresenceOptions _appliedDiscordOptions;
    private string? _shownFanartApiKey;

    public IntegrationsSettingsViewModel(SettingsStore settings)
    {
        _settings = settings;
        _appliedDiscordPresenceEnabled = settings.Current.DiscordPresenceEnabled;
        _appliedDiscordOptions = DiscordPresenceOptions.From(settings.Current);
        ShowSavedValues();
    }

    // Off by default: the status tells everyone on Discord what you're listening to.
    [ObservableProperty]
    public partial bool DiscordPresenceEnabled { get; set; }

    [ObservableProperty]
    public partial bool DiscordShowArtist { get; set; }

    [ObservableProperty]
    public partial bool DiscordShowAlbum { get; set; }

    [ObservableProperty]
    public partial bool DiscordShowElapsed { get; set; }

    [ObservableProperty]
    public partial bool DiscordShowWhilePaused { get; set; }

    // The applied values - what the rest of the app acts on, not the checkboxes as they are being edited.
    public bool IsDiscordPresenceEnabled => _appliedDiscordPresenceEnabled;

    public DiscordPresenceOptions AppliedDiscordOptions => _appliedDiscordOptions;

    // The status was switched on or off, or what it shows changed.
    public event EventHandler? DiscordPresenceChanged;

    private DiscordPresenceOptions EditedDiscordOptions =>
        new(DiscordShowArtist, DiscordShowAlbum, DiscordShowElapsed, DiscordShowWhilePaused);

    [ObservableProperty]
    public partial string? FanartApiKey { get; set; }

    private bool HasFanartApiKeyEdit =>
        (string.IsNullOrWhiteSpace(FanartApiKey) ? null : FanartApiKey.Trim()) != _shownFanartApiKey;

    public string DisplayName => Strings.SettingsCategoryIntegrations;

    public bool HasPendingChanges =>
        DiscordPresenceEnabled != _appliedDiscordPresenceEnabled || EditedDiscordOptions != _appliedDiscordOptions || HasFanartApiKeyEdit;

    ICommand ISettingsCategoryViewModel.ApplyCommand => ApplyIntegrationsCommand;

    ICommand ISettingsCategoryViewModel.CancelCommand => CancelIntegrationsCommand;

    partial void OnDiscordPresenceEnabledChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnDiscordShowArtistChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnDiscordShowAlbumChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnDiscordShowElapsedChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnDiscordShowWhilePausedChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnFanartApiKeyChanged(string? value) => OnPropertyChanged(nameof(HasPendingChanges));

    [RelayCommand]
    private void ApplyIntegrations()
    {
        var enabledChanged = DiscordPresenceEnabled != _appliedDiscordPresenceEnabled;
        var options = EditedDiscordOptions;
        var optionsChanged = options != _appliedDiscordOptions;
        var fanartApiKey = HasFanartApiKeyEdit ? FanartApiKey : _settings.Current.FanartApiKey;
        _settings.Update(s => s with
        {
            DiscordPresenceEnabled = DiscordPresenceEnabled,
            DiscordShowArtist = options.ShowArtist,
            DiscordShowAlbum = options.ShowAlbum,
            DiscordShowElapsed = options.ShowElapsed,
            DiscordShowWhilePaused = options.ShowWhilePaused,
            FanartApiKey = fanartApiKey,
        });
        _appliedDiscordPresenceEnabled = DiscordPresenceEnabled;
        _appliedDiscordOptions = options;
        ShowSavedFanartApiKey();
        OnPropertyChanged(nameof(HasPendingChanges));

        if (enabledChanged)
            OnPropertyChanged(nameof(IsDiscordPresenceEnabled));

        if (enabledChanged || optionsChanged)
            DiscordPresenceChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CancelIntegrations() => ShowSavedValues();

    public void ShowSavedValues()
    {
        DiscordPresenceEnabled = _appliedDiscordPresenceEnabled;
        DiscordShowArtist = _appliedDiscordOptions.ShowArtist;
        DiscordShowAlbum = _appliedDiscordOptions.ShowAlbum;
        DiscordShowElapsed = _appliedDiscordOptions.ShowElapsed;
        DiscordShowWhilePaused = _appliedDiscordOptions.ShowWhilePaused;
        ShowSavedFanartApiKey();
    }

    // The cover search can save a key too, so only an edit of what was shown overwrites the stored one.
    private void ShowSavedFanartApiKey()
    {
        _shownFanartApiKey = _settings.Current.FanartApiKey;
        FanartApiKey = _shownFanartApiKey;
    }
}
