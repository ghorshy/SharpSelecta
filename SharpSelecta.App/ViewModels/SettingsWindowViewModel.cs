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

    public SettingsWindowViewModel(LibraryViewModel library)
    {
        Library = library;
        selectedCategory = Categories[0];
    }
}
