using Avalonia.Controls;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class SidePanelLayoutViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static (SidePanelLayoutViewModel Layout, InterfaceSettingsViewModel Settings) Create(string settingsPath)
    {
        var store = new SettingsStore(settingsPath);
        var settings = new InterfaceSettingsViewModel(store, Substitute.For<IFilePickerService>());
        return (new SidePanelLayoutViewModel(store, TestThemeLayout.Default, settings), settings);
    }

    private static void Choose(InterfaceSettingsViewModel settings, SidePanelPosition position) =>
        settings.SelectedSidePanelPositionOption = settings.SidePanelPositionOptions.Single(o => o.Position == position);

    [Test]
    public async Task ByDefault_ThePanelIsOnTheRight_QueueAboveNowPlaying_WithCoverAndDetails()
    {
        var (layout, _) = Create(_temp.NewSettingsPath());

        await Assert.That(layout.IsOnRight).IsTrue();
        await Assert.That(layout.Column).IsEqualTo(4);
        await Assert.That(layout.RightWidth.Value).IsEqualTo(220);
        await Assert.That(layout.LeftWidth).IsEqualTo(GridLength.Auto);
        await Assert.That(layout.NowPlayingDock).IsEqualTo(Dock.Bottom);
        await Assert.That(layout.ShowsArtwork).IsTrue();
        await Assert.That(layout.ShowsDetails).IsTrue();
    }

    [Test]
    public async Task TheWidth_PersistsAcrossInstancesForTheSameSettingsFile()
    {
        var settingsPath = _temp.NewSettingsPath();
        var (layout, _) = Create(settingsPath);

        layout.RightWidth = new GridLength(300);
        layout.PersistWidth();

        await Assert.That(Create(settingsPath).Layout.RightWidth.Value).IsEqualTo(300);
    }

    [Test]
    public async Task ChoosingLeft_MovesThePanelAndItsWidthBeforeApply()
    {
        var (layout, settings) = Create(_temp.NewSettingsPath());
        var changed = new List<string?>();
        layout.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Choose(settings, SidePanelPosition.Left);

        await Assert.That(layout.Column).IsEqualTo(0);
        await Assert.That(layout.LeftWidth.Value).IsEqualTo(220);
        await Assert.That(layout.RightWidth).IsEqualTo(GridLength.Auto);
        await Assert.That(changed).Contains(nameof(SidePanelLayoutViewModel.Column));
    }

    [Test]
    public async Task Cancel_PutsThePanelBack()
    {
        var (layout, settings) = Create(_temp.NewSettingsPath());

        Choose(settings, SidePanelPosition.Left);
        settings.ShowNowPlayingArtwork = false;
        settings.CancelCommand.Execute(null);

        await Assert.That(layout.IsOnRight).IsTrue();
        await Assert.That(layout.ShowsArtwork).IsTrue();
    }

    [Test]
    public async Task Hidden_LeavesNoColumnForThePanel_AndGreysOutItsOtherOptions()
    {
        var (layout, settings) = Create(_temp.NewSettingsPath());

        Choose(settings, SidePanelPosition.Hidden);

        await Assert.That(layout.IsVisible).IsFalse();
        await Assert.That(layout.IsOnLeft).IsFalse();
        await Assert.That(layout.IsOnRight).IsFalse();
        await Assert.That(layout.LeftWidth).IsEqualTo(GridLength.Auto);
        await Assert.That(layout.RightWidth).IsEqualTo(GridLength.Auto);
        await Assert.That(settings.IsSidePanelShown).IsFalse();
    }

    [Test]
    public async Task TheEmptyColumn_NeverChangesTheWidth()
    {
        var (layout, settings) = Create(_temp.NewSettingsPath());
        Choose(settings, SidePanelPosition.Left);

        layout.RightWidth = new GridLength(500);

        await Assert.That(layout.LeftWidth.Value).IsEqualTo(220);
    }

    [Test]
    public async Task QueueBelow_PutsNowPlayingOnTop()
    {
        var (layout, settings) = Create(_temp.NewSettingsPath());

        settings.SelectedQueuePositionOption = settings.QueuePositionOptions.Single(o => o.Position == QueuePosition.Bottom);

        await Assert.That(layout.IsQueueAtBottom).IsTrue();
        await Assert.That(layout.NowPlayingDock).IsEqualTo(Dock.Top);
    }

    [Test]
    public async Task HidingCoverAndDetails_HidesNowPlaying()
    {
        var (layout, settings) = Create(_temp.NewSettingsPath());

        settings.ShowNowPlayingArtwork = false;
        await Assert.That(layout.ShowsNowPlaying).IsTrue();

        settings.ShowNowPlayingDetails = false;
        await Assert.That(layout.ShowsNowPlaying).IsFalse();
    }
}
