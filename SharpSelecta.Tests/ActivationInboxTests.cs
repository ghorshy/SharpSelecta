using NSubstitute;
using SharpSelecta.App.Services.SingleInstance;

namespace SharpSelecta.Tests;

public class ActivationInboxTests
{
    private static void Raise(ISingleInstanceGuard guard, params string[] paths) =>
        guard.ActivationRequested += NSubstitute.Raise.Event<EventHandler<IReadOnlyList<string>>>(guard, (IReadOnlyList<string>)paths);

    [Test]
    public async Task RequestsArrivingBeforeAttach_AreDeliveredOnAttachInOrder_ThenLiveOnes()
    {
        var guard = Substitute.For<ISingleInstanceGuard>();
        var inbox = new ActivationInbox(guard);
        Raise(guard, "a.mp3");
        Raise(guard, "b.mp3");

        var received = new List<string>();
        inbox.Attach(paths => received.Add(string.Join(",", paths)));
        Raise(guard, "c.mp3");

        await Assert.That(string.Join("|", received)).IsEqualTo("a.mp3|b.mp3|c.mp3");
    }
}
