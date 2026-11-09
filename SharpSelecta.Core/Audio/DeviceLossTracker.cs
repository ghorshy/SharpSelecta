namespace SharpSelecta.Core.Audio;

// The system may move our stream to a fallback before or after it reports the old device removed; both orders count, once.
public sealed class DeviceLossTracker<TId>(TimeProvider? time = null) where TId : notnull
{
    private static readonly TimeSpan RemovalAfterMoveWindow = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private TId? _current;
    private bool _hasCurrent;
    private (TId Device, DateTimeOffset At)? _movedAwayFrom;

    public bool StreamMovedTo(TId device, Func<TId, bool> deviceExists)
    {
        var lost = false;
        if (_hasCurrent && !Is(_current!, device))
        {
            lost = !deviceExists(_current!);
            _movedAwayFrom = lost ? null : (_current!, _time.GetUtcNow());
        }

        _current = device;
        _hasCurrent = true;
        return lost;
    }

    public void StreamSwitchedTo(TId device)
    {
        _current = device;
        _hasCurrent = true;
        _movedAwayFrom = null;
    }

    public bool DeviceRemoved(TId device)
    {
        if (_hasCurrent && Is(_current!, device))
        {
            _hasCurrent = false;
            return true;
        }

        if (_movedAwayFrom is { } movedAway && Is(movedAway.Device, device) &&
            _time.GetUtcNow() - movedAway.At <= RemovalAfterMoveWindow)
        {
            _movedAwayFrom = null;
            return true;
        }

        return false;
    }

    private static bool Is(TId a, TId b) => EqualityComparer<TId>.Default.Equals(a, b);
}
