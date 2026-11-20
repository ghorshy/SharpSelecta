using SharpSelecta.App.Views;

namespace SharpSelecta.Tests;

public class TrackListViewColumnWidthTests
{
    [Test]
    [Arguments(1d)] // an Auto column's Width.Value, which is how every column once got saved as 1 px
    [Arguments(0d)]
    [Arguments(19.5d)]
    [Arguments(double.NaN)]
    [Arguments(double.PositiveInfinity)]
    public async Task IsUsableWidth_RejectsWidthsThatWouldHideTheColumn(double width)
    {
        await Assert.That(TrackListView.IsUsableWidth(width, 20)).IsFalse();
    }

    [Test]
    [Arguments(20d)]
    [Arguments(150d)]
    public async Task IsUsableWidth_AcceptsRealWidths(double width)
    {
        await Assert.That(TrackListView.IsUsableWidth(width, 20)).IsTrue();
    }
}
