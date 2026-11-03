using System.Windows.Input;

namespace SharpSelecta.App.ViewModels;

public interface ISettingsCategoryViewModel
{
    string DisplayName { get; }

    bool HasPendingChanges { get; }

    ICommand ApplyCommand { get; }

    ICommand CancelCommand { get; }
}
