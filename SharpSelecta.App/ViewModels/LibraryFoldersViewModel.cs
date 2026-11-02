using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed partial class LibraryFoldersViewModel : ViewModelBase
{
    private readonly IFilePickerService _filePickerService;
    private readonly SettingsStore _settings;
    private readonly Func<Task> _onFoldersChanged;

    public ObservableCollection<string> Paths { get; } = [];

    public bool HasFolders => Paths.Count > 0;

    public ObservableCollection<string> PendingPaths { get; } = [];

    public bool HasPendingFolders => PendingPaths.Count > 0;

    public bool HasPendingChanges => !PendingPaths.SequenceEqual(Paths);

    public LibraryFoldersViewModel(IFilePickerService filePickerService, SettingsStore settings, Func<Task> onFoldersChanged)
    {
        _filePickerService = filePickerService;
        _settings = settings;
        _onFoldersChanged = onFoldersChanged;

        Paths.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasFolders));
            OnPropertyChanged(nameof(HasPendingChanges));
            SyncPendingPaths();
        };

        PendingPaths.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPendingFolders));
            OnPropertyChanged(nameof(HasPendingChanges));
        };
    }

    public void Restore(IEnumerable<string> folderPaths)
    {
        foreach (var folderPath in folderPaths)
        {
            Paths.Add(folderPath);
        }
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folderPath = await _filePickerService.PickLibraryFolderAsync();
        if (folderPath is null || Paths.Contains(folderPath))
            return;

        Paths.Add(folderPath);
        Save();
        await _onFoldersChanged();
    }

    [RelayCommand]
    private async Task AddPendingFolderAsync()
    {
        var folderPath = await _filePickerService.PickLibraryFolderAsync();
        if (folderPath is null || PendingPaths.Contains(folderPath))
            return;

        PendingPaths.Add(folderPath);
    }

    [RelayCommand]
    private void RemovePendingFolder(string folderPath) => PendingPaths.Remove(folderPath);

    [RelayCommand]
    private async Task ApplyPendingChangesAsync()
    {
        if (!HasPendingChanges)
            return;

        // ToList() snapshots before Clear() re-syncs PendingPaths as a side effect.
        var folderPaths = PendingPaths.ToList();

        Paths.Clear();
        Restore(folderPaths);

        Save();
        await _onFoldersChanged();
    }

    [RelayCommand]
    private void CancelPendingChanges() => SyncPendingPaths();

    private void SyncPendingPaths()
    {
        PendingPaths.Clear();
        foreach (var folderPath in Paths)
        {
            PendingPaths.Add(folderPath);
        }
    }

    private void Save() => _settings.Update(s => s with { LibraryFolderPaths = Paths.ToList() });
}
