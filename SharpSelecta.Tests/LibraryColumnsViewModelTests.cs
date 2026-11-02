using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class LibraryColumnsViewModelTests
{
    private readonly string _settingsPath =
        Path.Combine(Path.GetTempPath(), $"sharpselecta-columns-vm-tests-{Guid.NewGuid():N}.json");

    [After(Test)]
    public void DeleteSettingsFile() => File.Delete(_settingsPath);

    [Test]
    public async Task HidingEveryColumn_LeavesTheLastOneVisible()
    {
        var columns = new LibraryColumnsViewModel(new SettingsStore(_settingsPath));

        foreach (var column in columns.All)
        {
            column.IsVisible = false;
        }

        await Assert.That(columns.All.Count(c => c.IsVisible)).IsEqualTo(1);
        await Assert.That(columns["Year"].IsVisible).IsTrue();
    }

    [Test]
    public async Task HidingAColumn_WhileAnotherIsStillVisible_Succeeds()
    {
        var columns = new LibraryColumnsViewModel(new SettingsStore(_settingsPath));

        columns["TrackNumber"].IsVisible = false;

        await Assert.That(columns["TrackNumber"].IsVisible).IsFalse();
        await Assert.That(columns["Title"].IsVisible).IsTrue();
    }

    [Test]
    public async Task Visibility_PersistsAcrossInstancesForTheSameSettingsFile()
    {
        var columns = new LibraryColumnsViewModel(new SettingsStore(_settingsPath));
        columns["Artist"].IsVisible = false;
        columns["Year"].IsVisible = false;

        var restarted = new LibraryColumnsViewModel(new SettingsStore(_settingsPath));

        await Assert.That(restarted["Artist"].IsVisible).IsFalse();
        await Assert.That(restarted["Year"].IsVisible).IsFalse();
        await Assert.That(restarted["Title"].IsVisible).IsTrue();
    }

    [Test]
    public async Task NewerColumns_AreHiddenByDefault_AndTheirVisibilityIsSaved()
    {
        var columns = new LibraryColumnsViewModel(new SettingsStore(_settingsPath));
        await Assert.That(columns["Genre"].IsVisible).IsFalse();
        await Assert.That(columns["AlbumArtist"].IsVisible).IsFalse();
        await Assert.That(columns["DiscNumber"].IsVisible).IsFalse();

        columns["Genre"].IsVisible = true;

        var saved = new SettingsStore(_settingsPath).Current.Columns!;
        await Assert.That(saved["Genre"]).IsTrue();
        await Assert.That(saved["AlbumArtist"]).IsFalse();
    }

    [Test]
    public async Task All_HasOneColumnPerKey_InDisplayOrder()
    {
        var columns = new LibraryColumnsViewModel(new SettingsStore(_settingsPath));

        await Assert.That(columns.All.Select(c => c.Key)).IsEquivalentTo(
            ["TrackNumber", "DiscNumber", "Title", "Artist", "AlbumArtist", "Album", "Genre", "Length", "SampleRate", "BitDepth", "Bitrate", "FileType", "Year"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task SavedLayoutWithEveryColumnHidden_FallsBackToTheDefaults()
    {
        new SettingsStore(_settingsPath).Update(s => s with { Columns = new Dictionary<string, bool> { ["Title"] = false, ["Year"] = false } });

        var columns = new LibraryColumnsViewModel(new SettingsStore(_settingsPath));

        await Assert.That(columns["Title"].IsVisible).IsTrue();
        await Assert.That(columns["Genre"].IsVisible).IsFalse();
    }

    [Test]
    public async Task NotifyLayoutChanged_RaisesLayoutChanged()
    {
        var columns = new LibraryColumnsViewModel(new SettingsStore(_settingsPath));
        var raised = false;
        columns.LayoutChanged += (_, _) => raised = true;

        columns.NotifyLayoutChanged();

        await Assert.That(raised).IsTrue();
    }
}
