using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class QueueStateStoreTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-queue-state-tests-{Guid.NewGuid():N}.json");

    private static string QueueStateFilePath(string settingsPath) =>
        Path.Combine(Path.GetDirectoryName(settingsPath)!, $"{Path.GetFileNameWithoutExtension(settingsPath)}.queue-state.json");

    [Test]
    public async Task Load_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = QueueStateStore.Load(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsQueueState()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            QueueStateStore.QueueEntryData[] entries =
            [
                new("/music/a.mp3", QueueEntrySource.Manual),
                new("/music/b.mp3", QueueEntrySource.AutoDj),
            ];

            QueueStateStore.Save(settingsPath, entries, 1, 42.5);

            var loaded = QueueStateStore.Load(settingsPath);

            await Assert.That(loaded!.Entries).IsEquivalentTo(entries);
            await Assert.That(loaded.CurrentIndex).IsEqualTo(1);
            await Assert.That(loaded.PositionSeconds).IsEqualTo(42.5);
        }
        finally
        {
            File.Delete(QueueStateFilePath(settingsPath));
        }
    }

    [Test]
    public async Task Save_WithNoEntries_MakesLoadReturnNull()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            QueueStateStore.Save(settingsPath, [new QueueStateStore.QueueEntryData("/music/a.mp3", QueueEntrySource.Manual)], 0, 0);

            QueueStateStore.Save(settingsPath, [], -1, 0);

            await Assert.That(QueueStateStore.Load(settingsPath)).IsNull();
        }
        finally
        {
            File.Delete(QueueStateFilePath(settingsPath));
        }
    }

    [Test]
    public async Task Save_WritesOnlyToItsOwnSiblingFile_NeverTheSettingsFile()
    {
        var settingsPath = CreateTempSettingsPath();
        var queueStatePath = QueueStateFilePath(settingsPath);
        try
        {
            SettingsStore.SaveLibraryFolderPaths(settingsPath, ["/music/library"]);
            SettingsStore.SaveRestoreQueueOnStartup(settingsPath, false);
            var settingsContentBeforeQueueSave = File.ReadAllText(settingsPath);

            QueueStateStore.Save(settingsPath, [new QueueStateStore.QueueEntryData("/music/a.mp3", QueueEntrySource.Manual)], 0, 10);

            await Assert.That(File.Exists(queueStatePath)).IsTrue();
            await Assert.That(File.ReadAllText(settingsPath)).IsEqualTo(settingsContentBeforeQueueSave);
            await Assert.That(SettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo(["/music/library"]);
            await Assert.That(SettingsStore.LoadRestoreQueueOnStartup(settingsPath)).IsFalse();
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(queueStatePath);
        }
    }
}
