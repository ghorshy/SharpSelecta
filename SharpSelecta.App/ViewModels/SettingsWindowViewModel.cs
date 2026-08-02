using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.App.Resources;

namespace SharpSelecta.App.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    public IReadOnlyList<string> Categories { get; } = [Strings.SettingsCategoryLibrary, Strings.SettingsCategoryPlayback];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCategoryViewModel))]
    private string selectedCategory;

    public LibraryViewModel Library { get; }

    public PlaybackSettingsViewModel Playback { get; }

    public ISettingsCategoryViewModel SelectedCategoryViewModel =>
        SelectedCategory == Strings.SettingsCategoryPlayback ? Playback : Library;

    public SettingsWindowViewModel(LibraryViewModel library, PlaybackSettingsViewModel playback)
    {
        Library = library;
        Playback = playback;
        selectedCategory = Categories[0];
    }
}
