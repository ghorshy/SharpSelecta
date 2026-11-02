using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Updates;

namespace SharpSelecta.App.ViewModels;

// The Updates settings category and the "ready to install" bar in the main window. Without an update service
// that supports this install (Linux, portable zip) everything here stays inert and the category is hidden.
public sealed partial class UpdatesViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    private readonly SettingsStore _settings;
    private readonly IUpdateService? _updateService;
    private readonly ILogger _logger;
    private bool _appliedCheckAutomatically;

    public UpdatesViewModel(SettingsStore settings, IUpdateService? updateService, ILogger logger)
    {
        _settings = settings;
        _updateService = updateService;
        _logger = logger;
        _appliedCheckAutomatically = settings.Current.CheckForUpdatesAutomatically;
        CheckAutomatically = _appliedCheckAutomatically;
    }

    public bool IsSupported => _updateService?.IsSupported == true;

    public string? CurrentVersionDisplay => _updateService?.CurrentVersion is { } version ? Strings.CurrentVersion(version) : null;

    [ObservableProperty]
    public partial bool CheckAutomatically { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    public partial bool IsChecking { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdateReady), nameof(ShowUpdateBar), nameof(UpdateReadyMessage))]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    public partial string? ReadyVersion { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateBar))]
    public partial bool IsUpdateBarDismissed { get; set; }

    public bool IsUpdateReady => ReadyVersion is not null;

    public bool ShowUpdateBar => IsUpdateReady && !IsUpdateBarDismissed;

    public string? UpdateReadyMessage => ReadyVersion is { } version ? Strings.UpdateReady(version) : null;

    public bool HasPendingChanges => CheckAutomatically != _appliedCheckAutomatically;

    ICommand ISettingsCategoryViewModel.ApplyCommand => ApplyUpdatesSettingsCommand;

    ICommand ISettingsCategoryViewModel.CancelCommand => CancelUpdatesSettingsCommand;

    partial void OnCheckAutomaticallyChanged(bool value) => OnPropertyChanged(nameof(HasPendingChanges));

    public Task CheckOnStartupAsync() =>
        IsSupported && _appliedCheckAutomatically ? CheckAsync() : Task.CompletedTask;

    private bool CanCheckNow => IsSupported && !IsChecking && !IsUpdateReady;

    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    private Task CheckNow() => CheckAsync();

    private async Task CheckAsync()
    {
        IsChecking = true;
        StatusMessage = Strings.CheckingForUpdates;
        try
        {
            var version = await _updateService!.DownloadLatestAsync(CancellationToken.None);
            ReadyVersion = version;
            StatusMessage = version is null ? Strings.UpToDate : Strings.UpdateReady(version);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Update check failed");
            StatusMessage = Strings.UpdateCheckFailed;
        }
        finally
        {
            IsChecking = false;
        }
    }

    // The updater ends the process itself, so the app's normal exit handling never runs - this is its chance to save state.
    public event EventHandler? RestartingToUpdate;

    [RelayCommand]
    private void RestartToUpdate()
    {
        RestartingToUpdate?.Invoke(this, EventArgs.Empty);
        _updateService?.RestartToUpdate();
    }

    [RelayCommand]
    private void DismissUpdateBar() => IsUpdateBarDismissed = true;

    // A downloaded update the user didn't restart for is installed once the app closes.
    public void InstallPendingUpdateOnExit()
    {
        if (IsUpdateReady)
        {
            _updateService?.UpdateOnExit();
        }
    }

    [RelayCommand]
    private void ApplyUpdatesSettings()
    {
        _settings.Update(s => s with { CheckForUpdatesAutomatically = CheckAutomatically });
        _appliedCheckAutomatically = CheckAutomatically;
        OnPropertyChanged(nameof(HasPendingChanges));
    }

    [RelayCommand]
    private void CancelUpdatesSettings() => CheckAutomatically = _appliedCheckAutomatically;
}
