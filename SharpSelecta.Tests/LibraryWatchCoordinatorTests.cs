using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class LibraryWatchCoordinatorTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ManualTimeProvider _time = new();
    private readonly FakeLibraryChangeSource _source = new();
    private readonly List<Task> _rescans = [];

    public void Dispose() => _temp.Dispose();

    private async Task<(LibraryViewModel Library, LibraryWatchCoordinator Watch, DirectoryInfo Root, IFilePickerService Picker)> CreateAsync(bool watchSetting = true)
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        new SettingsStore(settingsPath).Update(s => s with { LibraryFolderPaths = [root.FullName], WatchLibraryFolders = watchSetting });
        var settings = new SettingsStore(settingsPath);
        var picker = Substitute.For<IFilePickerService>();
        var playback = new PlaybackControlsViewModel(Substitute.For<IAudioEngine>(), new PlaybackQueue(), settings, NullLogger<PlaybackControlsViewModel>.Instance);
        var library = new LibraryViewModel(picker, playback, Substitute.For<IFileManagerService>(), settings, TestThemeLayout.Default, NullLogger<LibraryViewModel>.Instance);
        await library.InitializeAsync();

        var watch = new LibraryWatchCoordinator(library, _source, work =>
        {
            var rescan = work();
            _rescans.Add(rescan);
            return rescan;
        }, NullLogger.Instance, _time);
        return (library, watch, root, picker);
    }

    [Test]
    public async Task WatchesTheLibraryFolders_OnlyOnceStarted()
    {
        var (_, watch, root, _) = await CreateAsync();
        await Assert.That(_source.Roots).IsEmpty();

        watch.Start();

        await Assert.That(_source.Roots).IsEquivalentTo([root.FullName]);
    }

    [Test]
    public async Task AChangeInAFolder_RescansTheLibrary()
    {
        var (library, watch, root, _) = await CreateAsync();
        watch.Start();
        var path = Path.Combine(root.FullName, "new.mp3");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), path);

        _source.Raise(new LibraryChange(path, IsDirectory: false));
        _time.Advance(LibraryChangeDetector.QuietPeriod);
        await Task.WhenAll(_rescans);

        await Assert.That(library.Tracks.Select(t => t.Track.FilePath)).IsEquivalentTo([path]);
    }

    [Test]
    public async Task TheSettingOff_StopsWatching_AndApplyingItOnStartsAgain()
    {
        var (library, watch, root, _) = await CreateAsync(watchSetting: false);
        watch.Start();
        await Assert.That(_source.Roots).IsEmpty();

        library.WatchFolders = true;
        await Assert.That(_source.Roots).IsEmpty();

        await library.ApplySettingsCommand.ExecuteAsync(null);
        await Assert.That(_source.Roots).IsEquivalentTo([root.FullName]);
    }

    [Test]
    public async Task AddingAFolder_WatchesItToo()
    {
        var (library, watch, root, picker) = await CreateAsync();
        watch.Start();
        var added = _temp.NewSubdirectory();
        picker.PickLibraryFolderAsync().Returns(added.FullName);

        await library.Folders.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(_source.Roots).IsEquivalentTo([root.FullName, added.FullName]);
    }

    [Test]
    public async Task Dispose_StopsWatching()
    {
        var (_, watch, _, _) = await CreateAsync();
        watch.Start();

        watch.Dispose();

        await Assert.That(_source.Roots).IsEmpty();
    }
}
