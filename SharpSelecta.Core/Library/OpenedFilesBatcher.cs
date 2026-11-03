namespace SharpSelecta.Core.Library;

// Windows starts one process per file for a multi-file "Open with", so their requests arrive
// separately and in random order; merging the ones that arrive close together opens them as one.
public sealed class OpenedFilesBatcher(TimeSpan window, TimeProvider time, Action<IReadOnlyList<string>> open)
{
    private readonly Lock _lock = new();
    private readonly List<IReadOnlyList<string>> _pending = [];
    private ITimer? _timer;

    public void Add(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
            return;

        lock (_lock)
        {
            _pending.Add(paths);
            _timer?.Dispose();
            _timer = time.CreateTimer(_ => Flush(), null, window, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        List<string> paths;
        lock (_lock)
        {
            paths = _pending.OrderBy(request => request[0], StringComparer.OrdinalIgnoreCase).SelectMany(request => request).ToList();
            _pending.Clear();
            _timer?.Dispose();
            _timer = null;
        }

        if (paths.Count > 0)
        {
            open(paths);
        }
    }
}
