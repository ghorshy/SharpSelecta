namespace SharpSelecta.Tests;

// A clock the test moves by hand; timers fire when Advance carries the time past their due moment.
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public int PendingTimers => _timers.Count(t => !t.Disposed);

    public void Advance(TimeSpan by)
    {
        _now += by;
        foreach (var timer in _timers.Where(t => !t.Disposed && t.DueAt <= _now).ToList())
        {
            timer.Fire();
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, _now + dueTime);
        _timers.Add(timer);
        return timer;
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
    {
        public DateTimeOffset DueAt { get; private set; } = dueAt;

        public bool Disposed { get; private set; }

        public void Fire()
        {
            Disposed = true; // one-shot, like the throttle's use of it
            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
