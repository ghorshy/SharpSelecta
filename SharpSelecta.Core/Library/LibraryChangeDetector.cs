namespace SharpSelecta.Core.Library;

// Turns file-system events under the library folders into "reconcile now" calls: a burst fires once,
// QuietPeriod after its last event but no later than MaxDelay after its first, and never while a
// reconcile is still running - a burst that ends meanwhile fires once that one is done.
public sealed class LibraryChangeDetector : IDisposable
{
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);

    private readonly ILibraryChangeSource _source;
    private readonly TimeProvider _time;
    private readonly Func<Task> _reconcile;
    private readonly Action<Exception> _onError;
    private readonly Lock _lock = new();
    private ITimer? _timer;
    private DateTimeOffset? _burstStart;
    private bool _running;
    private bool _fireWhenDone;
    private bool _disposed;

    public LibraryChangeDetector(ILibraryChangeSource source, TimeProvider time, Func<Task> reconcile, Action<Exception> onError)
    {
        _source = source;
        _time = time;
        _reconcile = reconcile;
        _onError = onError;
        _source.Changed += OnChanged;
        _source.Failed += OnFailed;
    }

    public void Watch(IReadOnlyCollection<string> roots)
    {
        if (roots.Count == 0)
        {
            lock (_lock)
            {
                CancelBurst();
            }
        }

        _source.Watch(roots);
    }

    private static bool IsRelevant(LibraryChange change) =>
        change.IsDirectory || MusicLibraryScanner.IsAudioFile(change.Path) ||
        (change.OldPath is { } oldPath && MusicLibraryScanner.IsAudioFile(oldPath));

    private void OnChanged(object? sender, LibraryChange change)
    {
        if (!IsRelevant(change))
            return;

        lock (_lock)
        {
            if (_disposed)
                return;

            var now = _time.GetUtcNow();
            _burstStart ??= now;
            var fireAt = Min(now + QuietPeriod, _burstStart.Value + MaxDelay);
            _timer?.Dispose();
            _timer = _time.CreateTimer(_ => OnBurstEnded(), null, fireAt - now, Timeout.InfiniteTimeSpan);
        }
    }

    // Events were lost, so whatever is pending can't be trusted to describe the changes: reconcile now.
    private void OnFailed(object? sender, string root)
    {
        _source.Recreate(root);
        OnBurstEnded();
    }

    private void OnBurstEnded()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            CancelBurst();
            if (_running)
            {
                _fireWhenDone = true;
                return;
            }

            _running = true;
        }

        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        while (true)
        {
            try
            {
                await _reconcile().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _onError(ex);
            }

            lock (_lock)
            {
                if (!_fireWhenDone || _disposed)
                {
                    _running = false;
                    return;
                }

                _fireWhenDone = false;
            }
        }
    }

    private void CancelBurst()
    {
        _timer?.Dispose();
        _timer = null;
        _burstStart = null;
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            CancelBurst();
        }

        _source.Changed -= OnChanged;
        _source.Failed -= OnFailed;
        _source.Watch([]);
    }
}
