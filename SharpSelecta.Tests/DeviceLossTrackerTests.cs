using SharpSelecta.Core.Audio;

namespace SharpSelecta.Tests;

public class DeviceLossTrackerTests
{
    [Test]
    public async Task RemovingTheDeviceTheStreamIsOn_IsALoss()
    {
        var tracker = new DeviceLossTracker<int>();
        tracker.StreamMovedTo(70, _ => true);

        await Assert.That(tracker.DeviceRemoved(70)).IsTrue();
    }

    [Test]
    public async Task RemovingAnotherDevice_IsNotALoss()
    {
        var tracker = new DeviceLossTracker<int>();
        tracker.StreamMovedTo(70, _ => true);

        await Assert.That(tracker.DeviceRemoved(66)).IsFalse();
    }

    [Test]
    public async Task ARemovalBeforeAnyStreamWasSeen_IsNotALoss()
    {
        var tracker = new DeviceLossTracker<int>();

        await Assert.That(tracker.DeviceRemoved(70)).IsFalse();
    }

    [Test]
    public async Task TheStreamMovingAwayFromADeviceThatIsGone_IsALoss()
    {
        var tracker = new DeviceLossTracker<int>();
        tracker.StreamMovedTo(70, _ => true);

        await Assert.That(tracker.StreamMovedTo(66, device => device != 70)).IsTrue();
    }

    [Test]
    public async Task TheStreamMovingWhileTheOldDeviceStillExists_IsNotALoss()
    {
        var tracker = new DeviceLossTracker<int>();
        tracker.StreamMovedTo(70, _ => true);

        await Assert.That(tracker.StreamMovedTo(66, _ => true)).IsFalse();
    }

    [Test]
    public async Task TheRemovalAfterAReportedMove_IsNotReportedAgain()
    {
        var tracker = new DeviceLossTracker<int>();
        tracker.StreamMovedTo(70, _ => true);
        tracker.StreamMovedTo(66, device => device != 70);

        await Assert.That(tracker.DeviceRemoved(70)).IsFalse();
    }

    [Test]
    public async Task TheMoveAfterAReportedRemoval_IsNotReportedAgain()
    {
        var tracker = new DeviceLossTracker<int>();
        tracker.StreamMovedTo(70, _ => true);
        tracker.DeviceRemoved(70);

        await Assert.That(tracker.StreamMovedTo(66, device => device != 70)).IsFalse();
    }

    [Test]
    public async Task TheStreamStayingOnTheSameDevice_IsNotALoss()
    {
        var tracker = new DeviceLossTracker<int>();
        tracker.StreamMovedTo(70, _ => true);

        await Assert.That(tracker.StreamMovedTo(70, _ => false)).IsFalse();
    }

    [Test]
    public async Task TheStreamMovingAwayJustBeforeTheOldDeviceIsRemoved_IsALoss()
    {
        var time = new ManualTimeProvider();
        var tracker = new DeviceLossTracker<int>(time);
        tracker.StreamMovedTo(70, _ => true);
        tracker.StreamMovedTo(66, _ => true);
        time.Advance(TimeSpan.FromSeconds(1));

        await Assert.That(tracker.DeviceRemoved(70)).IsTrue();
    }

    [Test]
    public async Task RemovingADeviceLongAfterTheStreamMovedAway_IsNotALoss()
    {
        var time = new ManualTimeProvider();
        var tracker = new DeviceLossTracker<int>(time);
        tracker.StreamMovedTo(70, _ => true);
        tracker.StreamMovedTo(66, _ => true);
        time.Advance(TimeSpan.FromMinutes(1));

        await Assert.That(tracker.DeviceRemoved(70)).IsFalse();
    }

    [Test]
    public async Task RemovingTheDeviceWeSwitchedAwayFrom_IsNotALoss()
    {
        var tracker = new DeviceLossTracker<int>();
        tracker.StreamMovedTo(70, _ => true);
        tracker.StreamSwitchedTo(66);

        await Assert.That(tracker.DeviceRemoved(70)).IsFalse();
    }

    [Test]
    public async Task RemovingTheDeviceWeSwitchedTo_IsALoss()
    {
        var tracker = new DeviceLossTracker<int>();
        tracker.StreamMovedTo(70, _ => true);
        tracker.StreamSwitchedTo(66);

        await Assert.That(tracker.DeviceRemoved(66)).IsTrue();
    }
}
