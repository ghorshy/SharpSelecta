using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class OpenedFilesTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string CreateFile(params string[] segments)
    {
        var path = Path.Combine([_temp.Path, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, []);
        return path;
    }

    [Test]
    public async Task Expand_KeepsAudioFilesAndIgnoresOtherExtensionsAndMissingPaths()
    {
        var song = CreateFile("song.FLAC");
        var picture = CreateFile("cover.jpg");

        var files = OpenedFiles.Expand([song, picture, Path.Combine(_temp.Path, "missing.mp3"), "--some-flag"]);

        await Assert.That(files).IsEquivalentTo([song]);
    }

    [Test]
    public async Task Expand_FolderBecomesItsAudioFilesRecursively()
    {
        var b = CreateFile("album", "b.mp3");
        var a = CreateFile("album", "cd1", "a.ogg");
        CreateFile("album", "notes.txt");

        var files = OpenedFiles.Expand([Path.Combine(_temp.Path, "album")]);

        await Assert.That(string.Join("|", files)).IsEqualTo(string.Join("|", b, a));
    }

    [Test]
    public async Task Expand_PlaylistBecomesItsExistingAudioEntriesInOrder()
    {
        var second = CreateFile("music", "z.mp3");
        var first = CreateFile("music", "a.wav");
        var playlist = _temp.FilePath("mix.m3u");
        File.WriteAllText(playlist, $"#EXTM3U\n{Path.Combine("music", "z.mp3")}\n{first}\nmissing.mp3\n{_temp.FilePath("x.txt")}\n");

        var files = OpenedFiles.Expand([playlist]);

        await Assert.That(string.Join("|", files)).IsEqualTo(string.Join("|", second, first));
    }

    [Test]
    public async Task Expand_ConcatenatesArgumentsInTheOrderGiven()
    {
        var single = CreateFile("single.mp3");
        var inFolder = CreateFile("folder", "track.flac");
        var listed = CreateFile("listed.m4a");
        var playlist = _temp.FilePath("list.m3u8");
        File.WriteAllText(playlist, listed);

        var files = OpenedFiles.Expand([playlist, single, Path.Combine(_temp.Path, "folder")]);

        await Assert.That(string.Join("|", files)).IsEqualTo(string.Join("|", listed, single, inFolder));
    }

    [Test]
    public async Task ToAbsoluteArguments_ResolvesRelativePathsAgainstTheWorkingDirectory()
    {
        var paths = OpenedFiles.ToAbsoluteArguments([Path.Combine("sub", "a.mp3"), TestPaths.Music("b.mp3")], _temp.Path);

        await Assert.That(string.Join("|", paths))
            .IsEqualTo(string.Join("|", Path.Combine(_temp.Path, "sub", "a.mp3"), TestPaths.Music("b.mp3")));
    }
}
