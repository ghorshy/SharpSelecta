using SharpSelecta.Core.Library;

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
}
