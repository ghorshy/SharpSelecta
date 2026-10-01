namespace SharpSelecta.Core.Presence;

// Discord allows only a handful of presence updates per half minute and drops the rest, so changes are
// coalesced: the first goes out at once, later ones wait until the interval has passed and only the
// latest state is sent. Disposing clears the status immediately (the app is closing).
public sealed class ThrottledRichPresence(IRichPresence inner, TimeSpan minimumInterval, TimeProvider time) : IRichPresence, IDisposable
{
    private readonly Lock _lock = new();
    private PresenceActivity? _desired;
    private PresenceActivity? _lastSent;
    private bool _everSent;
    private DateTimeOffset _lastSentAt = DateTimeOffset.MinValue;
    private ITimer? _timer;
    private bool _disposed;

    public void Show(PresenceActivity activity) => Request(activity);

    public void Clear() => Request(null);

    private void Request(PresenceActivity? desired)
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _desired = desired;
            var wait = _lastSentAt + minimumInterval - time.GetUtcNow();
            if (wait <= TimeSpan.Zero)
            {
                CancelTimer();
                SendDesired();
            }
            else if (_timer is null)
            {
                _timer = time.CreateTimer(_ => Flush(), null, wait, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void Flush()
    {
        lock (_lock)
        {
            CancelTimer();
            if (!_disposed)
            {
                SendDesired();
            }
        }
    }

    private void SendDesired()
    {
        // Nothing to say that wasn't just said (e.g. a Clear while already cleared).
        if (_everSent && Equals(_desired, _lastSent))
            return;

        _lastSentAt = time.GetUtcNow();
        _lastSent = _desired;
        _everSent = true;
        if (_desired is { } activity)
        {
            inner.Show(activity);
        }
        else
        {
            inner.Clear();
        }
    }

    private void CancelTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            CancelTimer();
            inner.Clear();
        }

        (inner as IDisposable)?.Dispose();
    }
}
