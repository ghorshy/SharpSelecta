using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class LibrarySettingsStoreTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-settings-tests-{Guid.NewGuid():N}.json");

    [Test]
    public async Task SaveAndLoad_RoundTripsTheFolderPaths()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library", "/music/other"]);

            var loaded = LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath);

            await Assert.That(loaded).IsEquivalentTo(["/music/library", "/music/other"]);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task Load_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task Load_WhenFileIsCorrupted_ReturnsNullInsteadOfThrowing()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "{ not valid json");

            var loaded = LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath);

            await Assert.That(loaded).IsNull();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task Save_OverwritesThePreviousFolderPaths()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/old"]);
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/new"]);

            var loaded = LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath);

            await Assert.That(loaded).IsEquivalentTo(["/music/new"]);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsColumnVisibility()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var columns = new Dictionary<string, bool>
            {
                ["TrackNumber"] = true, ["Title"] = true, ["Artist"] = false, ["Album"] = false,
                ["Length"] = true, ["SampleRate"] = false, ["BitDepth"] = false, ["Bitrate"] = true,
                ["FileType"] = false, ["Year"] = true,
            };

            LibrarySettingsStore.SaveColumnVisibility(settingsPath, columns);

            var loaded = LibrarySettingsStore.LoadColumnVisibility(settingsPath);

            await Assert.That(loaded).IsEquivalentTo(columns);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SavingColumnVisibility_DoesNotClobberAnAlreadySavedFolderPaths()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library"]);

            LibrarySettingsStore.SaveColumnVisibility(settingsPath, new Dictionary<string, bool> { ["TrackNumber"] = true });

            await Assert.That(LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo(["/music/library"]);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SavingFolderPaths_DoesNotClobberAnAlreadySavedColumnVisibility()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var columns = new Dictionary<string, bool> { ["TrackNumber"] = true, ["Title"] = false };
            LibrarySettingsStore.SaveColumnVisibility(settingsPath, columns);

            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library"]);

            await Assert.That(LibrarySettingsStore.LoadColumnVisibility(settingsPath)).IsEquivalentTo(columns);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadColumnVisibility_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadColumnVisibility(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsColumnOrder()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            string[] order = ["Artist", "Title", "TrackNumber", "Year"];

            LibrarySettingsStore.SaveColumnOrder(settingsPath, order);

            var loaded = LibrarySettingsStore.LoadColumnOrder(settingsPath);

            await Assert.That(loaded).IsEquivalentTo(order);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SavingColumnOrder_DoesNotClobberAnAlreadySavedFolderPathsOrColumnVisibility()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library"]);
            var columns = new Dictionary<string, bool> { ["TrackNumber"] = true, ["Title"] = false };
            LibrarySettingsStore.SaveColumnVisibility(settingsPath, columns);

            LibrarySettingsStore.SaveColumnOrder(settingsPath, ["Title", "Artist"]);

            await Assert.That(LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo(["/music/library"]);
            await Assert.That(LibrarySettingsStore.LoadColumnVisibility(settingsPath)).IsEquivalentTo(columns);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadColumnOrder_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadColumnOrder(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsRightColumnWidth()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveRightColumnWidth(settingsPath, 275.5);

            var loaded = LibrarySettingsStore.LoadRightColumnWidth(settingsPath);

            await Assert.That(loaded).IsEqualTo(275.5);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsColumnWidths()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var widths = new Dictionary<string, double> { ["Title"] = 250, ["Artist"] = 180 };

            LibrarySettingsStore.SaveColumnWidths(settingsPath, widths);

            var loaded = LibrarySettingsStore.LoadColumnWidths(settingsPath);

            await Assert.That(loaded!["Title"]).IsEqualTo(250);
            await Assert.That(loaded!["Artist"]).IsEqualTo(180);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SavingRightColumnWidth_DoesNotClobberOtherSettings()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library"]);
            LibrarySettingsStore.SaveColumnOrder(settingsPath, ["Title", "Artist"]);

            LibrarySettingsStore.SaveRightColumnWidth(settingsPath, 300);

            await Assert.That(LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo(["/music/library"]);
            await Assert.That(LibrarySettingsStore.LoadColumnOrder(settingsPath)).IsEquivalentTo(["Title", "Artist"]);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsSort()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveSort(settingsPath, "Track.Bitrate", true);

            var loaded = LibrarySettingsStore.LoadSort(settingsPath);

            await Assert.That(loaded).IsEqualTo(("Track.Bitrate", true));
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadSort_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadSort(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SavingSort_DoesNotClobberOtherSettings()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library"]);
            LibrarySettingsStore.SaveColumnOrder(settingsPath, ["Title", "Artist"]);

            LibrarySettingsStore.SaveSort(settingsPath, "Track.Title", false);

            await Assert.That(LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo(["/music/library"]);
            await Assert.That(LibrarySettingsStore.LoadColumnOrder(settingsPath)).IsEquivalentTo(["Title", "Artist"]);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsTileSize()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveTileSize(settingsPath, 220);

            var loaded = LibrarySettingsStore.LoadTileSize(settingsPath);

            await Assert.That(loaded).IsEqualTo(220);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadTileSize_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadTileSize(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsViewMode()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveViewMode(settingsPath, LibraryViewMode.AlbumGrid);

            var loaded = LibrarySettingsStore.LoadViewMode(settingsPath);

            await Assert.That(loaded).IsEqualTo(LibraryViewMode.AlbumGrid);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadViewMode_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadViewMode(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsAlbumSortMode()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveAlbumSortMode(settingsPath, AlbumSortMode.Year);

            var loaded = LibrarySettingsStore.LoadAlbumSortMode(settingsPath);

            await Assert.That(loaded).IsEqualTo(AlbumSortMode.Year);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadAlbumSortMode_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadAlbumSortMode(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsAlbumSortDescending()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveAlbumSortDescending(settingsPath, true);

            var loaded = LibrarySettingsStore.LoadAlbumSortDescending(settingsPath);

            await Assert.That(loaded).IsTrue();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadAlbumSortDescending_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadAlbumSortDescending(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SavingAlbumSortMode_DoesNotClobberOtherSettings()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library"]);
            LibrarySettingsStore.SaveTileSize(settingsPath, 200);

            LibrarySettingsStore.SaveAlbumSortMode(settingsPath, AlbumSortMode.Artist);
            LibrarySettingsStore.SaveAlbumSortDescending(settingsPath, true);

            await Assert.That(LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo(["/music/library"]);
            await Assert.That(LibrarySettingsStore.LoadTileSize(settingsPath)).IsEqualTo(200);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SavingTileSizeAndViewMode_DoesNotClobberOtherSettings()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library"]);
            LibrarySettingsStore.SaveSort(settingsPath, "Track.Title", false);

            LibrarySettingsStore.SaveTileSize(settingsPath, 180);
            LibrarySettingsStore.SaveViewMode(settingsPath, LibraryViewMode.AlbumGrid);

            await Assert.That(LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo(["/music/library"]);
            await Assert.That(LibrarySettingsStore.LoadSort(settingsPath)).IsEqualTo(("Track.Title", false));
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadRestoreQueueOnStartup_WhenFileDoesNotExist_DefaultsToTrue()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadRestoreQueueOnStartup(settingsPath);

        await Assert.That(loaded).IsTrue();
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsRestoreQueueOnStartup()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveRestoreQueueOnStartup(settingsPath, false);

            var loaded = LibrarySettingsStore.LoadRestoreQueueOnStartup(settingsPath);

            await Assert.That(loaded).IsFalse();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadQueueState_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadQueueState(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsQueueState()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.QueueEntryData[] entries =
            [
                new("/music/a.mp3", QueueEntrySource.Manual),
                new("/music/b.mp3", QueueEntrySource.AutoDj),
            ];

            LibrarySettingsStore.SaveQueueState(settingsPath, entries, 1, 42.5);

            var loaded = LibrarySettingsStore.LoadQueueState(settingsPath);

            await Assert.That(loaded!.Entries).IsEquivalentTo(entries);
            await Assert.That(loaded.CurrentIndex).IsEqualTo(1);
            await Assert.That(loaded.PositionSeconds).IsEqualTo(42.5);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SaveQueueState_WithNoEntries_MakesLoadQueueStateReturnNull()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveQueueState(settingsPath, [new LibrarySettingsStore.QueueEntryData("/music/a.mp3", QueueEntrySource.Manual)], 0, 0);

            LibrarySettingsStore.SaveQueueState(settingsPath, [], -1, 0);

            await Assert.That(LibrarySettingsStore.LoadQueueState(settingsPath)).IsNull();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SavingQueueState_DoesNotClobberOtherSettings()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library"]);
            LibrarySettingsStore.SaveRestoreQueueOnStartup(settingsPath, false);

            LibrarySettingsStore.SaveQueueState(settingsPath, [new LibrarySettingsStore.QueueEntryData("/music/a.mp3", QueueEntrySource.Manual)], 0, 10);

            await Assert.That(LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo(["/music/library"]);
            await Assert.That(LibrarySettingsStore.LoadRestoreQueueOnStartup(settingsPath)).IsFalse();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadOutputDeviceName_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadOutputDeviceName(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsOutputDeviceName()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveOutputDeviceName(settingsPath, "Focusrite Scarlett 2i2");

            var loaded = LibrarySettingsStore.LoadOutputDeviceName(settingsPath);

            await Assert.That(loaded).IsEqualTo("Focusrite Scarlett 2i2");
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SaveOutputDeviceName_WithNull_RoundTripsBackToNull()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveOutputDeviceName(settingsPath, "Focusrite Scarlett 2i2");

            LibrarySettingsStore.SaveOutputDeviceName(settingsPath, null);

            await Assert.That(LibrarySettingsStore.LoadOutputDeviceName(settingsPath)).IsNull();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SavingOutputDeviceName_DoesNotClobberOtherSettings()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library"]);

            LibrarySettingsStore.SaveOutputDeviceName(settingsPath, "Focusrite Scarlett 2i2");

            await Assert.That(LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo(["/music/library"]);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadVolume_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadVolume(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsVolume()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveVolume(settingsPath, 0.35);

            var loaded = LibrarySettingsStore.LoadVolume(settingsPath);

            await Assert.That(loaded).IsEqualTo(0.35);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task LoadVolumeCurve_WhenFileDoesNotExist_DefaultsToLinear()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibrarySettingsStore.LoadVolumeCurve(settingsPath);

        await Assert.That(loaded).IsEqualTo(VolumeCurve.Linear);
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsVolumeCurve()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveVolumeCurve(settingsPath, VolumeCurve.Logarithmic);

            var loaded = LibrarySettingsStore.LoadVolumeCurve(settingsPath);

            await Assert.That(loaded).IsEqualTo(VolumeCurve.Logarithmic);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SavingVolumeAndVolumeCurve_DoesNotClobberOtherSettings()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library"]);

            LibrarySettingsStore.SaveVolume(settingsPath, 0.7);
            LibrarySettingsStore.SaveVolumeCurve(settingsPath, VolumeCurve.Logarithmic);

            await Assert.That(LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo(["/music/library"]);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }
}
