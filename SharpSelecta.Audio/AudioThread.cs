using System.Collections.Concurrent;

namespace SharpSelecta.Audio;

public static class AudioThread
{
    private static readonly BlockingCollection<Action> Queue = new();
    private static readonly Thread Worker = StartWorker();

    private static Thread StartWorker()
    {
        var thread = new Thread(() =>
        {
            foreach (var work in Queue.GetConsumingEnumerable())
            {
                work();
            }
        })
        {
            IsBackground = true,
            Name = "SharpSelecta audio",
        };
        if (OperatingSystem.IsWindows())
            thread.SetApartmentState(ApartmentState.MTA);

        thread.Start();
        return thread;
    }

    public static bool IsCurrent => Thread.CurrentThread == Worker;

    public static Task<T> InvokeAsync<T>(Func<T> work)
    {
        if (IsCurrent)
            return Task.FromResult(work());

        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue.Add(() =>
        {
            try
            {
                done.SetResult(work());
            }
            catch (Exception exception)
            {
                done.SetException(exception);
            }
        });
        return done.Task;
    }

    public static Task InvokeAsync(Action work) => InvokeAsync(() =>
    {
        work();
        return true;
    });

    public static T Invoke<T>(Func<T> work) => IsCurrent ? work() : InvokeAsync(work).GetAwaiter().GetResult();

    public static void Invoke(Action work) => InvokeAsync(work).GetAwaiter().GetResult();
}
