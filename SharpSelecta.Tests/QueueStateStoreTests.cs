using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class QueueStateStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Test]
    public async Task Load_WhenFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = _temp.NewSettingsPath();

        var loaded = QueueStateStore.Load(settingsPath);

        await Assert.That(loaded).IsNull();
    }

    [Test]
    public async Task SaveAndLoad_RoundTripsQueueState()
    {
        var settingsPath = _temp.NewSettingsPath();
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

    [Test]
    public async Task Save_WithNoEntries_MakesLoadReturnNull()
    {
        var settingsPath = _temp.NewSettingsPath();
        QueueStateStore.Save(settingsPath, [new QueueStateStore.QueueEntryData("/music/a.mp3", QueueEntrySource.Manual)], 0, 0);

        QueueStateStore.Save(settingsPath, [], -1, 0);

        await Assert.That(QueueStateStore.Load(settingsPath)).IsNull();
    }

    [Test]
    public async Task Save_WritesOnlyToItsOwnSiblingFile_NeverTheSettingsFile()
    {
        var settingsPath = _temp.NewSettingsPath();
        var queueStatePath = QueueStateStore.QueueStateFilePath(settingsPath);
        new SettingsStore(settingsPath).Update(s => s with { LibraryFolderPaths = ["/music/library"] });
        new SettingsStore(settingsPath).Update(s => s with { RestoreQueueOnStartup = false });
        var settingsContentBeforeQueueSave = File.ReadAllText(settingsPath);

        QueueStateStore.Save(settingsPath, [new QueueStateStore.QueueEntryData("/music/a.mp3", QueueEntrySource.Manual)], 0, 10);

        await Assert.That(File.Exists(queueStatePath)).IsTrue();
        await Assert.That(File.ReadAllText(settingsPath)).IsEqualTo(settingsContentBeforeQueueSave);
        await Assert.That(new SettingsStore(settingsPath).Current.LibraryFolderPaths).IsEquivalentTo(["/music/library"]);
        await Assert.That(new SettingsStore(settingsPath).Current.RestoreQueueOnStartup).IsFalse();
    }
}
