using Avalonia;
using SharpSelecta.App.Views;

namespace SharpSelecta.Tests;

public class AlbumCoverWindowTests
{
    [Test]
    public async Task Fit_KeepsACoverThatFitsAtItsOwnSize()
    {
        await Assert.That(AlbumCoverWindow.Fit(new Size(600, 600), new Size(1700, 950))).IsEqualTo(new Size(600, 600));
    }

    [Test]
    public async Task Fit_ShrinksABigCoverKeepingItsProportions()
    {
        await Assert.That(AlbumCoverWindow.Fit(new Size(3000, 3000), new Size(1700, 950))).IsEqualTo(new Size(950, 950));
        await Assert.That(AlbumCoverWindow.Fit(new Size(2000, 1000), new Size(1000, 950))).IsEqualTo(new Size(1000, 500));
    }
}
