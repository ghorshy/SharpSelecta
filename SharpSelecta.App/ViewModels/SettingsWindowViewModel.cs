using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;

namespace SharpSelecta.App.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    public IReadOnlyList<string> Categories { get; } =
        [Strings.SettingsCategoryLibrary, Strings.SettingsCategoryPlayback, Strings.SettingsCategoryInterface, Strings.SettingsCategoryKeyboardShortcuts];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCategoryViewModel))]
    private string selectedCategory;

    public LibraryViewModel Library { get; }

    public PlaybackSettingsViewModel Playback { get; }

    public InterfaceSettingsViewModel Interface { get; }

    public KeyboardShortcutsViewModel KeyboardShortcuts { get; }

    public ISettingsCategoryViewModel SelectedCategoryViewModel => SelectedCategory switch
    {
        _ when SelectedCategory == Strings.SettingsCategoryPlayback => Playback,
        _ when SelectedCategory == Strings.SettingsCategoryInterface => Interface,
        _ when SelectedCategory == Strings.SettingsCategoryKeyboardShortcuts => KeyboardShortcuts,
        _ => Library,
    };

    public SettingsWindowViewModel(
        LibraryViewModel library, PlaybackSettingsViewModel playback, InterfaceSettingsViewModel interfaceSettings, ShortcutSettingsService shortcutSettings)
    {
        Library = library;
        Playback = playback;
        Interface = interfaceSettings;
        KeyboardShortcuts = new KeyboardShortcutsViewModel(shortcutSettings);
        selectedCategory = Categories[0];
    }
}
