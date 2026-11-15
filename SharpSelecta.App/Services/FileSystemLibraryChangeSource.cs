using Microsoft.Extensions.Logging;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services;

// Two watchers per root, one for files and one for folders, so every event says which it was
// (a deleted path can't be checked afterwards).
public sealed class FileSystemLibraryChangeSource(ILogger<FileSystemLibraryChangeSource> logger, TimeProvider? time = null)
    : ILibraryChangeSource, IDisposable
{
    // A root that fails again this soon after being recreated (e.g. inotify's max_user_watches
    // exhausted) is given up on rather than recreated in a loop; it stays on F5.
    private static readonly TimeSpan GiveUpWindow = TimeSpan.FromMinutes(1);

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, FileSystemWatcher[]> _watchers = new(PathComparer);
    private readonly Dictionary<string, DateTimeOffset> _lastFailure = new(PathComparer);

    public event EventHandler<LibraryChange>? Changed;

    public event EventHandler<string>? Failed;

    public void Watch(IReadOnlyCollection<string> roots)
    {
        lock (_lock)
        {
            foreach (var root in _watchers.Keys.Where(root => !roots.Contains(root, PathComparer)).ToList())
            {
                Stop(root);
            }

            foreach (var root in roots.Where(root => !_watchers.ContainsKey(root)))
            {
                TryStart(root);
            }
        }
    }

    public void Recreate(string root)
    {
        lock (_lock)
        {
            if (!_watchers.ContainsKey(root))
                return;

            Stop(root);
            var now = _time.GetUtcNow();
            if (_lastFailure.TryGetValue(root, out var last) && now - last < GiveUpWindow)
            {
                logger.LogWarning("Watching {Root} keeps failing; changes there show up on F5 only", root);
                return;
            }

            _lastFailure[root] = now;
            TryStart(root);
        }
    }

    private void TryStart(string root)
    {
        var watchers = new List<FileSystemWatcher>();
        try
        {
            watchers.Add(Create(root, NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size, isDirectory: false));
            watchers.Add(Create(root, NotifyFilters.DirectoryName, isDirectory: true));
            _watchers[root] = [.. watchers];
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }

            logger.LogWarning(ex, "Can't watch {Root} for changes; changes there show up on F5 only", root);
        }
    }

    private FileSystemWatcher Create(string root, NotifyFilters filter, bool isDirectory)
    {
        var watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = filter,
            InternalBufferSize = 64 * 1024,
        };

        try
        {
            watcher.Created += (_, e) => Raise(new LibraryChange(e.FullPath, isDirectory));
            watcher.Deleted += (_, e) => Raise(new LibraryChange(e.FullPath, isDirectory));
            watcher.Renamed += (_, e) => Raise(new LibraryChange(e.FullPath, isDirectory, e.OldFullPath));
            if (!isDirectory)
                watcher.Changed += (_, e) => Raise(new LibraryChange(e.FullPath, isDirectory));

            watcher.Error += (_, e) =>
            {
                logger.LogWarning(e.GetException(), "Lost change events under {Root}", root);
                // Off the watcher's own thread: the handler recreates, i.e. disposes, this watcher.
                ThreadPool.QueueUserWorkItem(_ => Failed?.Invoke(this, root));
            };
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch
        {
            watcher.Dispose();
            throw;
        }
    }

    private void Raise(LibraryChange change) => Changed?.Invoke(this, change);

    private void Stop(string root)
    {
        if (_watchers.Remove(root, out var watchers))
        {
            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var root in _watchers.Keys.ToList())
            {
                Stop(root);
            }
        }
    }
}
