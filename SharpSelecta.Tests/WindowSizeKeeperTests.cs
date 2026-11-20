using Avalonia;
using Avalonia.Controls;
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

    [Test]
    [Arguments(WindowState.Normal, WindowState.Normal)]
    [Arguments(WindowState.Maximized, WindowState.Maximized)]
    [Arguments(WindowState.Minimized, WindowState.Normal)]
    public async Task RestoredState_LeavesFullScreenIntoTheStateBefore_ButNeverMinimized(WindowState before, WindowState expected)
    {
        await Assert.That(WindowSizeKeeper.RestoredState(before)).IsEqualTo(expected);
    }
}
