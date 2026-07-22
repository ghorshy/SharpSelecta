using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.App.Resources;

namespace SharpSelecta.App.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    public IReadOnlyList<string> Categories { get; } = [Strings.SettingsCategoryLibrary];

    [ObservableProperty]
    private string selectedCategory;

    public LibraryViewModel Library { get; }

    // Only one category exists so far, so this always resolves to Library — once a second
    // category is added, this switches on SelectedCategory to pick the right one.
    public ISettingsCategoryViewModel SelectedCategoryViewModel => Library;

    public SettingsWindowViewModel(LibraryViewModel library)
    {
        Library = library;
        selectedCategory = Categories[0];
    }
}
