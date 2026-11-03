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
            new(TestPaths.Music("a.mp3"), QueueEntrySource.Manual),
            new(TestPaths.Music("b.mp3"), QueueEntrySource.AutoDj),
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
        QueueStateStore.Save(settingsPath, [new QueueStateStore.QueueEntryData(TestPaths.Music("a.mp3"), QueueEntrySource.Manual)], 0, 0);

        QueueStateStore.Save(settingsPath, [], -1, 0);

        await Assert.That(QueueStateStore.Load(settingsPath)).IsNull();
    }

    [Test]
    public async Task Save_WritesOnlyToItsOwnSiblingFile_NeverTheSettingsFile()
    {
        var settingsPath = _temp.NewSettingsPath();
        var queueStatePath = QueueStateStore.QueueStateFilePath(settingsPath);
        new SettingsStore(settingsPath).Update(s => s with { LibraryFolderPaths = [TestPaths.Music("library")] });
        new SettingsStore(settingsPath).Update(s => s with { RestoreQueueOnStartup = false });
        var settingsContentBeforeQueueSave = File.ReadAllText(settingsPath);

        QueueStateStore.Save(settingsPath, [new QueueStateStore.QueueEntryData(TestPaths.Music("a.mp3"), QueueEntrySource.Manual)], 0, 10);

        await Assert.That(File.Exists(queueStatePath)).IsTrue();
        await Assert.That(File.ReadAllText(settingsPath)).IsEqualTo(settingsContentBeforeQueueSave);
        await Assert.That(new SettingsStore(settingsPath).Current.LibraryFolderPaths).IsEquivalentTo([TestPaths.Music("library")]);
        await Assert.That(new SettingsStore(settingsPath).Current.RestoreQueueOnStartup).IsFalse();
    }

    private static QueueStateStore.QueueEntryData Entry(string path, QueueEntrySource source = QueueEntrySource.Manual) => new(path, source);

    [Test]
    public async Task Resolve_DropsEntriesWhoseFileIsGone_KeepingOrderAndSource()
    {
        var state = new QueueStateStore.QueueState(
            [Entry(TestPaths.Fixture("tagged-track.mp3")), Entry(TestPaths.Music("gone.mp3")), Entry(TestPaths.Fixture("untagged-track.flac"), QueueEntrySource.AutoDj)],
            CurrentIndex: -1, PositionSeconds: 0);

        var resolved = state.Resolve();

        await Assert.That(string.Join("|", resolved.Entries.Select(e => $"{Path.GetFileName(e.Track.FilePath)}:{e.Source}")))
            .IsEqualTo("tagged-track.mp3:Manual|untagged-track.flac:AutoDj");
    }

    [Test]
    public async Task Resolve_CurrentIndexFollowsItsEntryPastDroppedOnes_AndKeepsThePosition()
    {
        var state = new QueueStateStore.QueueState(
            [Entry(TestPaths.Music("gone.mp3")), Entry(TestPaths.Fixture("tagged-track.mp3")), Entry(TestPaths.Fixture("untagged-track.flac"))],
            CurrentIndex: 2, PositionSeconds: 42.5);

        var resolved = state.Resolve();

        await Assert.That(resolved.CurrentIndex).IsEqualTo(1);
        await Assert.That(resolved.PositionSeconds).IsEqualTo(42.5);
    }

    [Test]
    public async Task Resolve_WhenTheCurrentFileIsGone_TheNextSurvivorBecomesCurrentFromTheStart()
    {
        var state = new QueueStateStore.QueueState(
            [Entry(TestPaths.Fixture("tagged-track.mp3")), Entry(TestPaths.Music("gone.mp3")), Entry(TestPaths.Music("also-gone.mp3")), Entry(TestPaths.Fixture("untagged-track.flac"))],
            CurrentIndex: 1, PositionSeconds: 42.5);

        var resolved = state.Resolve();

        await Assert.That(resolved.CurrentIndex).IsEqualTo(1);
        await Assert.That(resolved.Entries[resolved.CurrentIndex].Track.FilePath).IsEqualTo(TestPaths.Fixture("untagged-track.flac"));
        await Assert.That(resolved.PositionSeconds).IsEqualTo(0);
    }

    [Test]
    public async Task Resolve_WhenTheCurrentFileIsGoneAndNothingFollowsIt_TheLastSurvivorBecomesCurrentFromTheStart()
    {
        var state = new QueueStateStore.QueueState(
            [Entry(TestPaths.Fixture("tagged-track.mp3")), Entry(TestPaths.Fixture("untagged-track.flac")), Entry(TestPaths.Music("gone.mp3"))],
            CurrentIndex: 2, PositionSeconds: 42.5);

        var resolved = state.Resolve();

        await Assert.That(resolved.CurrentIndex).IsEqualTo(1);
        await Assert.That(resolved.PositionSeconds).IsEqualTo(0);
    }

    [Test]
    public async Task Resolve_WithASavedCurrentIndexPastTheEnd_TheLastSurvivorBecomesCurrentFromTheStart()
    {
        var state = new QueueStateStore.QueueState(
            [Entry(TestPaths.Fixture("tagged-track.mp3")), Entry(TestPaths.Fixture("untagged-track.flac"))],
            CurrentIndex: 5, PositionSeconds: 42.5);

        var resolved = state.Resolve();

        await Assert.That(resolved.CurrentIndex).IsEqualTo(1);
        await Assert.That(resolved.PositionSeconds).IsEqualTo(0);
    }

    [Test]
    public async Task Resolve_WithNoSavedCurrentEntry_TheFirstSurvivorBecomesCurrentFromTheStart()
    {
        var state = new QueueStateStore.QueueState(
            [Entry(TestPaths.Music("gone.mp3")), Entry(TestPaths.Fixture("tagged-track.mp3")), Entry(TestPaths.Fixture("untagged-track.flac"))],
            CurrentIndex: -1, PositionSeconds: 42.5);

        var resolved = state.Resolve();

        await Assert.That(resolved.CurrentIndex).IsEqualTo(0);
        await Assert.That(resolved.PositionSeconds).IsEqualTo(0);
    }
}
