using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class LibrarySettingsStoreTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-settings-tests-{Guid.NewGuid():N}.json");

    [Test]
    public async Task SaveAndLoad_RoundTripsTheFolderPath()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPath(settingsPath, "/music/library");

            var loaded = LibrarySettingsStore.LoadLibraryFolderPath(settingsPath);

            await Assert.That(loaded).IsEqualTo("/music/library");
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

        var loaded = LibrarySettingsStore.LoadLibraryFolderPath(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task Load_WhenFileIsCorrupted_ReturnsNullInsteadOfThrowing()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "{ not valid json");

            var loaded = LibrarySettingsStore.LoadLibraryFolderPath(settingsPath);

            await Assert.That(loaded).IsNull();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task Save_OverwritesThePreviousFolderPath()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            LibrarySettingsStore.SaveLibraryFolderPath(settingsPath, "/music/old");
            LibrarySettingsStore.SaveLibraryFolderPath(settingsPath, "/music/new");

            var loaded = LibrarySettingsStore.LoadLibraryFolderPath(settingsPath);

            await Assert.That(loaded).IsEqualTo("/music/new");
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }
}
