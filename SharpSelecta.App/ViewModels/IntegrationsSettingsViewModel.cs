using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// Settings for talking to other programs. Like the other categories, edits only take effect on Apply.
public sealed partial class IntegrationsSettingsViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    private readonly SettingsStore _settings;
    private bool _appliedDiscordPresenceEnabled;
    private string? _shownFanartApiKey;

    public IntegrationsSettingsViewModel(SettingsStore settings)
    {
        _settings = settings;
        _appliedDiscordPresenceEnabled = settings.Current.DiscordPresenceEnabled;
        ShowSavedValues();
    }

    // Off by default: the status tells everyone on Discord what you're listening to.
    [ObservableProperty]
    public partial bool DiscordPresenceEnabled { get; set; }

    // The applied value - what the rest of the app acts on, not the checkbox as it is being edited.
    public bool IsDiscordPresenceEnabled => _appliedDiscordPresenceEnabled;

    public event EventHandler? DiscordPresenceEnabledChanged;

    [ObservableProperty]
    public partial string? FanartApiKey { get; set; }

    private bool HasFanartApiKeyEdit =>
        (string.IsNullOrWhiteSpace(FanartApiKey) ? null : FanartApiKey.Trim()) != _shownFanartApiKey;

    public string DisplayName => Strings.SettingsCategoryIntegrations;

    public bool HasPendingChanges => DiscordPresenceEnabled != _appliedDiscordPresenceEnabled || HasFanartApiKeyEdit;

    ICommand ISettingsCategoryViewModel.ApplyCommand => ApplyIntegrationsCommand;

    ICommand ISettingsCategoryViewModel.CancelCommand => CancelIntegrationsCommand;

    partial void OnDiscordPresenceEnabledChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    partial void OnFanartApiKeyChanged(string? value) => OnPropertyChanged(nameof(HasPendingChanges));

    [RelayCommand]
    private void ApplyIntegrations()
    {
        var changed = DiscordPresenceEnabled != _appliedDiscordPresenceEnabled;
        var fanartApiKey = HasFanartApiKeyEdit ? FanartApiKey : _settings.Current.FanartApiKey;
        _settings.Update(s => s with { DiscordPresenceEnabled = DiscordPresenceEnabled, FanartApiKey = fanartApiKey });
        _appliedDiscordPresenceEnabled = DiscordPresenceEnabled;
        ShowSavedFanartApiKey();
        OnPropertyChanged(nameof(HasPendingChanges));

        if (changed)
        {
            OnPropertyChanged(nameof(IsDiscordPresenceEnabled));
            DiscordPresenceEnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void CancelIntegrations() => ShowSavedValues();

    public void ShowSavedValues()
    {
        DiscordPresenceEnabled = _appliedDiscordPresenceEnabled;
        ShowSavedFanartApiKey();
    }

    // The cover search can save a key too, so only an edit of what was shown overwrites the stored one.
    private void ShowSavedFanartApiKey()
    {
        _shownFanartApiKey = _settings.Current.FanartApiKey;
        FanartApiKey = _shownFanartApiKey;
    }
}
