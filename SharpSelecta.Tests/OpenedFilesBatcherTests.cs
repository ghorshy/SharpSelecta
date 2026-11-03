using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class OpenedFilesBatcherTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(400);

    private readonly ManualTimeProvider _time = new();
    private readonly List<string> _opened = [];

    private OpenedFilesBatcher CreateBatcher() => new(Window, _time, paths => _opened.Add(string.Join(",", paths)));

    [Test]
    public async Task RequestsCloseTogether_OpenOnce_OrderedByFirstPath_KeepingEachRequestsOwnOrder()
    {
        var batcher = CreateBatcher();

        batcher.Add(["c.mp3"]);
        _time.Advance(TimeSpan.FromMilliseconds(300));
        batcher.Add(["a.mp3", "z.mp3"]);
        _time.Advance(TimeSpan.FromMilliseconds(300));
        batcher.Add(["b.mp3"]);
        await Assert.That(_opened).IsEmpty();

        _time.Advance(Window);

        await Assert.That(string.Join("|", _opened)).IsEqualTo("a.mp3,z.mp3,b.mp3,c.mp3");
    }

    [Test]
    public async Task ARequestAfterTheWindow_OpensSeparately()
    {
        var batcher = CreateBatcher();

        batcher.Add(["a.mp3"]);
        _time.Advance(Window);
        batcher.Add(["b.mp3"]);
        _time.Advance(Window);

        await Assert.That(string.Join("|", _opened)).IsEqualTo("a.mp3|b.mp3");
    }

    [Test]
    public async Task AnEmptyRequest_OpensNothing()
    {
        var batcher = CreateBatcher();

        batcher.Add([]);
        _time.Advance(Window);

        await Assert.That(_opened).IsEmpty();
        await Assert.That(_time.PendingTimers).IsEqualTo(0);
    }
}
