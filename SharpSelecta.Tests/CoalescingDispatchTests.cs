using SharpSelecta.App.Services;

namespace SharpSelecta.Tests;

public class CoalescingDispatchTests
{
    private readonly Queue<Action> _posted = new();
    private readonly List<string> _applied = [];

    private CoalescingDispatch<int> Create() =>
        new(_posted.Enqueue, batch => _applied.Add(string.Join(",", batch)));

    [Test]
    public async Task ItemsAddedBeforeThePostedFlushRuns_AreAppliedTogetherInOnePost()
    {
        var dispatch = Create();

        dispatch.Add(1);
        dispatch.Add(2);
        dispatch.Add(3);

        await Assert.That(_posted.Count).IsEqualTo(1);
        _posted.Dequeue()();
        await Assert.That(string.Join("|", _applied)).IsEqualTo("1,2,3");
    }

    [Test]
    public async Task AnItemAddedAfterAFlush_PostsAgain()
    {
        var dispatch = Create();
        dispatch.Add(1);
        _posted.Dequeue()();

        dispatch.Add(2);

        await Assert.That(_posted.Count).IsEqualTo(1);
        _posted.Dequeue()();
        await Assert.That(string.Join("|", _applied)).IsEqualTo("1|2");
    }
}
