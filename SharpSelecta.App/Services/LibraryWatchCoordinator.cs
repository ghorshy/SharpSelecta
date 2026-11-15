using System.ComponentModel;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services;

// Watches the library folders while the setting is on and rescans them (the same path as F5) when
// something changes. Starts only once the startup reconcile is done.
public sealed class LibraryWatchCoordinator : IDisposable
{
    private readonly LibraryViewModel _library;
    private readonly LibraryChangeDetector _detector;
    private bool _started;

    // onUiThread runs the rescan on the dispatcher and completes when it has finished.
    public LibraryWatchCoordinator(
        LibraryViewModel library, ILibraryChangeSource source, Func<Func<Task>, Task> onUiThread, ILogger logger, TimeProvider? time = null)
    {
        _library = library;
        _detector = new LibraryChangeDetector(source, time ?? TimeProvider.System,
            () => onUiThread(() => library.RescanCommand.ExecuteAsync(null)),
            ex => logger.LogError(ex, "Rescan after a change in the library folders failed"));

        _library.Folders.PathsSaved += OnPathsSaved;
        _library.PropertyChanged += OnLibraryPropertyChanged;
    }

    public void Start()
    {
        _started = true;
        UpdateRoots();
    }

    private void OnPathsSaved(object? sender, EventArgs e) => UpdateRoots();

    private void OnLibraryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.IsWatchingFolders))
            UpdateRoots();
    }

    private void UpdateRoots() =>
        _detector.Watch(_started && _library.IsWatchingFolders ? [.. _library.Folders.Paths] : []);

    public void Dispose()
    {
        _library.Folders.PathsSaved -= OnPathsSaved;
        _library.PropertyChanged -= OnLibraryPropertyChanged;
        _detector.Dispose();
    }
}
