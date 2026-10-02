using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;

namespace SharpSelecta.App.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    // Updates is only listed where this install can update itself.
    public IReadOnlyList<string> Categories { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCategoryViewModel))]
    public partial string SelectedCategory { get; set; }
    public LibraryViewModel Library { get; }

    public PlaybackSettingsViewModel Playback { get; }

    public InterfaceSettingsViewModel Interface { get; }

    public IntegrationsSettingsViewModel Integrations { get; }

    public KeyboardShortcutsViewModel KeyboardShortcuts { get; }

    public UpdatesViewModel? Updates { get; }

    public ISettingsCategoryViewModel SelectedCategoryViewModel => SelectedCategory switch
    {
        _ when SelectedCategory == Strings.SettingsCategoryPlayback => Playback,
        _ when SelectedCategory == Strings.SettingsCategoryInterface => Interface,
        _ when SelectedCategory == Strings.SettingsCategoryIntegrations => Integrations,
        _ when SelectedCategory == Strings.SettingsCategoryKeyboardShortcuts => KeyboardShortcuts,
        _ when SelectedCategory == Strings.SettingsCategoryUpdates && Updates is not null => Updates,
        _ => Library,
    };

    public SettingsWindowViewModel(
        LibraryViewModel library, PlaybackSettingsViewModel playback, InterfaceSettingsViewModel interfaceSettings,
        IntegrationsSettingsViewModel integrations, ShortcutSettingsService shortcutSettings, UpdatesViewModel? updates = null)
    {
        Updates = updates is { IsSupported: true } ? updates : null;
        Categories = Updates is null
            ? [Strings.SettingsCategoryLibrary, Strings.SettingsCategoryPlayback, Strings.SettingsCategoryInterface, Strings.SettingsCategoryIntegrations, Strings.SettingsCategoryKeyboardShortcuts]
            : [Strings.SettingsCategoryLibrary, Strings.SettingsCategoryPlayback, Strings.SettingsCategoryInterface, Strings.SettingsCategoryIntegrations, Strings.SettingsCategoryKeyboardShortcuts, Strings.SettingsCategoryUpdates];
        Integrations = integrations;
        Library = library;
        Playback = playback;
        Interface = interfaceSettings;
        KeyboardShortcuts = new KeyboardShortcutsViewModel(shortcutSettings);
        SelectedCategory = Categories[0];
    }
}
