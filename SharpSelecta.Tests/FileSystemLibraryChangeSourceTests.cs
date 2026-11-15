using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

// Against the real file system: the watcher is the thin adapter, so there is nothing to fake below it.
public class FileSystemLibraryChangeSourceTests : IDisposable
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    private readonly TempDirectory _temp = new();
    private readonly FileSystemLibraryChangeSource _source = new(NullLogger<FileSystemLibraryChangeSource>.Instance);
    private readonly ConcurrentQueue<LibraryChange> _changes = new();

    public FileSystemLibraryChangeSourceTests() => _source.Changed += (_, change) => _changes.Enqueue(change);

    public void Dispose()
    {
        _source.Dispose();
        _temp.Dispose();
    }

    private async Task<LibraryChange?> WaitFor(Func<LibraryChange, bool> match)
    {
        var deadline = DateTime.UtcNow + EventTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_changes.FirstOrDefault(match) is { } change)
                return change;

            await Task.Delay(20);
        }

        return null;
    }

    [Test]
    public async Task ReportsFilesAndFolders_AsSuch_IncludingInSubfolders()
    {
        var root = _temp.NewSubdirectory();
        _source.Watch([root.FullName]);

        var album = Directory.CreateDirectory(Path.Combine(root.FullName, "Album"));
        var folderEvent = await WaitFor(c => c.Path == album.FullName);
        var file = Path.Combine(album.FullName, "a.mp3");
        await File.WriteAllBytesAsync(file, [1, 2, 3]);
        var fileEvent = await WaitFor(c => c.Path == file);

        await Assert.That(folderEvent?.IsDirectory).IsTrue();
        await Assert.That(fileEvent?.IsDirectory).IsFalse();
    }

    [Test]
    public async Task ARename_CarriesTheOldPath()
    {
        var root = _temp.NewSubdirectory();
        var oldPath = Path.Combine(root.FullName, "a.mp3.part");
        await File.WriteAllBytesAsync(oldPath, [1]);
        _source.Watch([root.FullName]);

        var newPath = Path.Combine(root.FullName, "a.mp3");
        File.Move(oldPath, newPath);

        await Assert.That((await WaitFor(c => c.Path == newPath))?.OldPath).IsEqualTo(oldPath);
    }

    [Test]
    public async Task AFolderThatCantBeWatched_IsSkipped_AndTheOthersStillAre()
    {
        var root = _temp.NewSubdirectory();
        _source.Watch([Path.Combine(root.FullName, "does-not-exist"), root.FullName]);

        var file = Path.Combine(root.FullName, "a.mp3");
        await File.WriteAllBytesAsync(file, [1]);

        await Assert.That(await WaitFor(c => c.Path == file)).IsNotNull();
    }

    [Test]
    public async Task AFolderNoLongerWatched_ReportsNothing()
    {
        var root = _temp.NewSubdirectory();
        var other = _temp.NewSubdirectory();
        _source.Watch([root.FullName, other.FullName]);
        _source.Watch([other.FullName]);

        await File.WriteAllBytesAsync(Path.Combine(root.FullName, "a.mp3"), [1]);
        var marker = Path.Combine(other.FullName, "marker.mp3");
        await File.WriteAllBytesAsync(marker, [1]);
        await WaitFor(c => c.Path == marker);

        await Assert.That(_changes.Any(c => c.Path.StartsWith(root.FullName))).IsFalse();
    }
}
