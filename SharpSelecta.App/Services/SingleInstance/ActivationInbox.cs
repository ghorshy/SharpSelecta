namespace SharpSelecta.App.Services.SingleInstance;

// The guard listens from Program.Main, long before the UI can handle a request, so early ones wait here.
public sealed class ActivationInbox
{
    private readonly Lock _lock = new();
    private readonly List<IReadOnlyList<string>> _pending = [];
    private Action<IReadOnlyList<string>>? _handler;

    public ActivationInbox(ISingleInstanceGuard guard) => guard.ActivationRequested += (_, paths) => Deliver(paths);

    public void Attach(Action<IReadOnlyList<string>> handler)
    {
        lock (_lock)
        {
            _handler = handler;
            foreach (var paths in _pending)
            {
                handler(paths);
            }

            _pending.Clear();
        }
    }

    private void Deliver(IReadOnlyList<string> paths)
    {
        lock (_lock)
        {
            if (_handler is null)
            {
                _pending.Add(paths);
            }
            else
            {
                _handler(paths);
            }
        }
    }
}
