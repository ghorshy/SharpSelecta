using Avalonia;
using SharpSelecta.App.Services;

namespace SharpSelecta.Tests;

public class WindowSizeKeeperTests
{
    [Test]
    public async Task Fit_KeepsASizeThatFits()
    {
        await Assert.That(WindowSizeKeeper.Fit(new Size(1280, 720), new Size(1920, 1050))).IsEqualTo(new Size(1280, 720));
    }

    [Test]
    public async Task Fit_ShrinksEachSideThatDoesNotFit()
    {
        await Assert.That(WindowSizeKeeper.Fit(new Size(2560, 1400), new Size(1920, 1050))).IsEqualTo(new Size(1920, 1050));
        await Assert.That(WindowSizeKeeper.Fit(new Size(1280, 1400), new Size(1920, 1050))).IsEqualTo(new Size(1280, 1050));
    }
}
