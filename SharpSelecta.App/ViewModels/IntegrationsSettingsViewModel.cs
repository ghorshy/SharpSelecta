using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// Settings for talking to other programs. Like the other categories, edits only take effect on Apply.
public sealed partial class IntegrationsSettingsViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    private readonly string _settingsFilePath;
    private bool _appliedDiscordPresenceEnabled;

    public IntegrationsSettingsViewModel(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
        _appliedDiscordPresenceEnabled = SettingsStore.LoadDiscordPresenceEnabled(settingsFilePath);
        DiscordPresenceEnabled = _appliedDiscordPresenceEnabled;
    }

    // Off by default: the status tells everyone on Discord what you're listening to.
    [ObservableProperty]
    public partial bool DiscordPresenceEnabled { get; set; }

    // The applied value - what the rest of the app acts on, not the checkbox as it is being edited.
    public bool IsDiscordPresenceEnabled => _appliedDiscordPresenceEnabled;

    public event EventHandler? DiscordPresenceEnabledChanged;

    public bool HasPendingChanges => DiscordPresenceEnabled != _appliedDiscordPresenceEnabled;

    ICommand ISettingsCategoryViewModel.ApplyCommand => ApplyIntegrationsCommand;

    ICommand ISettingsCategoryViewModel.CancelCommand => CancelIntegrationsCommand;

    partial void OnDiscordPresenceEnabledChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    [RelayCommand]
    private void ApplyIntegrations()
    {
        var changed = DiscordPresenceEnabled != _appliedDiscordPresenceEnabled;
        SettingsStore.SaveDiscordPresenceEnabled(_settingsFilePath, DiscordPresenceEnabled);
        _appliedDiscordPresenceEnabled = DiscordPresenceEnabled;
        OnPropertyChanged(nameof(HasPendingChanges));

        if (changed)
        {
            OnPropertyChanged(nameof(IsDiscordPresenceEnabled));
            DiscordPresenceEnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void CancelIntegrations() => DiscordPresenceEnabled = _appliedDiscordPresenceEnabled;
}
