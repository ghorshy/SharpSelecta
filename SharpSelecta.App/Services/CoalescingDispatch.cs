using System.Collections.Concurrent;

namespace SharpSelecta.App.Services;

public sealed class CoalescingDispatch<T>(Action<Action> post, Action<IReadOnlyList<T>> apply)
{
    private readonly ConcurrentQueue<T> _pending = new();
    private int _flushScheduled;

    public void Add(T item)
    {
        _pending.Enqueue(item);
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 0)
        {
            post(Flush);
        }
    }

    private void Flush()
    {
        Volatile.Write(ref _flushScheduled, 0);
        var batch = new List<T>();
        while (_pending.TryDequeue(out var item))
        {
            batch.Add(item);
        }

        if (batch.Count > 0)
        {
            apply(batch);
        }
    }
}
