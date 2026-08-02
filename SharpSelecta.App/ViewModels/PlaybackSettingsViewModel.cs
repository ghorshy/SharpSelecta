using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class PlaybackSettingsViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    private readonly string _settingsFilePath;

    [ObservableProperty]
    private bool restoreQueueOnStartup;

    // Toggle saves immediately, like the Library column-visibility checkboxes - there's nothing
    // expensive to batch behind Apply/Cancel here, unlike folder paths which trigger a rescan.
    public bool HasPendingChanges => false;

    public ICommand ApplyCommand { get; } = new RelayCommand(() => { });

    public ICommand CancelCommand { get; } = new RelayCommand(() => { });

    public PlaybackSettingsViewModel(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
        restoreQueueOnStartup = LibrarySettingsStore.LoadRestoreQueueOnStartup(settingsFilePath);
    }

    partial void OnRestoreQueueOnStartupChanged(bool value) =>
        LibrarySettingsStore.SaveRestoreQueueOnStartup(_settingsFilePath, value);
}
