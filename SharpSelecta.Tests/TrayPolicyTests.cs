using Avalonia.Controls;
using SharpSelecta.App.Services.Tray;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class TrayPolicyTests
{
    [Test]
    public async Task TheIcon_IsShownOnlyWhileOneOfTheTrayOptionsIsOn()
    {
        await Assert.That(TrayPolicy.ShowsIcon(new AppSettings())).IsFalse();
        await Assert.That(TrayPolicy.ShowsIcon(new AppSettings { MinimizeToTray = true })).IsTrue();
        await Assert.That(TrayPolicy.ShowsIcon(new AppSettings { CloseToTray = true })).IsTrue();
    }

    [Test]
    public async Task Minimizing_HidesTheWindowOnlyWhenTheOptionIsOnAndATrayIsThere()
    {
        var on = new AppSettings { MinimizeToTray = true };

        await Assert.That(TrayPolicy.HidesOnMinimize(on, trayAvailable: true)).IsTrue();
        await Assert.That(TrayPolicy.HidesOnMinimize(on, trayAvailable: false)).IsFalse();
        await Assert.That(TrayPolicy.HidesOnMinimize(new AppSettings(), trayAvailable: true)).IsFalse();
    }

    [Test]
    public async Task Closing_HidesTheWindowOnlyWhenTheUserClosesIt_WithTheOptionOnAndATrayThere()
    {
        var on = new AppSettings { CloseToTray = true };

        await Assert.That(TrayPolicy.HidesOnClose(on, true, WindowCloseReason.WindowClosing, isProgrammatic: false)).IsTrue();
        await Assert.That(TrayPolicy.HidesOnClose(on, false, WindowCloseReason.WindowClosing, isProgrammatic: false)).IsFalse();
        await Assert.That(TrayPolicy.HidesOnClose(new AppSettings(), true, WindowCloseReason.WindowClosing, isProgrammatic: false)).IsFalse();
        await Assert.That(TrayPolicy.HidesOnClose(on, true, WindowCloseReason.ApplicationShutdown, isProgrammatic: false)).IsFalse();
        await Assert.That(TrayPolicy.HidesOnClose(on, true, WindowCloseReason.OSShutdown, isProgrammatic: false)).IsFalse();
        await Assert.That(TrayPolicy.HidesOnClose(on, true, WindowCloseReason.WindowClosing, isProgrammatic: true)).IsFalse();
    }
}
