using System.Runtime.InteropServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;

namespace SharpSelecta.App.ViewModels;

public sealed partial class AboutViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    public string DisplayName => Strings.SettingsCategoryAbout;

    public bool HasPendingChanges => false;

    public ICommand ApplyCommand { get; } = new RelayCommand(() => { });

    public ICommand CancelCommand { get; } = new RelayCommand(() => { });

    public string VersionDisplay { get; } = Strings.CurrentVersion(AppVersion.Current);

    public string SystemDisplay { get; } = Strings.AboutSystem(
        RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture.ToString());

    // What "Copy version info" puts on the clipboard, for a bug report.
    public string VersionInfo => $"{Strings.AppName} {AppVersion.Current}{Environment.NewLine}{SystemDisplay}";

    public Uri RepositoryUri { get; } = new(AppIdentity.RepositoryUrl);

    public Uri IssuesUri { get; } = new(AppIdentity.RepositoryUrl + "/issues");

    public Uri LicenseUri { get; } = new(AppIdentity.RepositoryUrl + "/blob/main/LICENSE");

    public Uri LibrariesUri { get; } = new(AppIdentity.RepositoryUrl + "#built-with");

    public Uri LogsFolderUri { get; } = new(AppPaths.LogsDirectory);

    [ObservableProperty]
    public partial bool IsVersionInfoCopied { get; set; }
}
