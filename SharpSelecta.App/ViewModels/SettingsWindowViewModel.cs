using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.App.Services;

namespace SharpSelecta.App.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    public IReadOnlyList<ISettingsCategoryViewModel> Categories { get; }

    [ObservableProperty]
    public partial ISettingsCategoryViewModel SelectedCategory { get; set; }

    public KeyboardShortcutsViewModel KeyboardShortcuts { get; }

    public SettingsWindowViewModel(
        LibraryViewModel library, PlaybackSettingsViewModel playback, InterfaceSettingsViewModel interfaceSettings,
        IntegrationsSettingsViewModel integrations, ShortcutSettingsService shortcutSettings, UpdatesViewModel? updates = null)
    {
        KeyboardShortcuts = new KeyboardShortcutsViewModel(shortcutSettings);
        List<ISettingsCategoryViewModel> categories = [library, playback, interfaceSettings, integrations, KeyboardShortcuts];
        // Updates is only listed where this install can update itself.
        if (updates is { IsSupported: true })
            categories.Add(updates);

        Categories = categories;
        SelectedCategory = Categories[0];
    }
}
