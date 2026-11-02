using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class M3uPlaylistFileTests
{
    private static readonly string A = TestPaths.Music("a.mp3");
    private static readonly string B = TestPaths.Music("b.mp3");

    [Test]
    public async Task ParsePaths_SkipsCommentsAndBlankLines()
    {
        var content = $"#EXTM3U\n#EXTINF:200,Artist - Title\n{A}\n\n{B}\n";

        var paths = M3uPlaylistFile.ParsePaths(content, TestPaths.Music());

        await Assert.That(paths).IsEquivalentTo([A, B]);
    }

    [Test]
    [Arguments("songs/track.mp3")]
    [Arguments(@"songs\track.mp3")]
    public async Task ParsePaths_ResolvesRelativePathsAgainstTheBaseDirectory(string content)
    {
        var baseDirectory = TestPaths.Music();

        var paths = M3uPlaylistFile.ParsePaths(content, baseDirectory);

        await Assert.That(paths).IsEquivalentTo([Path.Combine(baseDirectory, "songs", "track.mp3")]);
    }

    [Test]
    public async Task ParsePaths_LeavesAbsolutePathsUnchanged()
    {
        var content = TestPaths.Rooted("absolute", "path", "track.mp3");

        var paths = M3uPlaylistFile.ParsePaths(content, TestPaths.Music());

        await Assert.That(paths).IsEquivalentTo([content]);
    }

    [Test]
    public async Task ParsePaths_HandlesCrlfLineEndings()
    {
        var content = $"{A}\r\n{B}\r\n";

        var paths = M3uPlaylistFile.ParsePaths(content, TestPaths.Music());

        await Assert.That(paths).IsEquivalentTo([A, B]);
    }

    [Test]
    public async Task Write_ProducesAValidM3u8WithExtinfLinesInOrder()
    {
        var tracks = new[]
        {
            (A, TimeSpan.FromSeconds(125), "Artist One", "Title One"),
            (B, TimeSpan.FromSeconds(200), "Artist Two", "Title Two"),
        };

        var content = M3uPlaylistFile.Write(tracks);
        var lines = content.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();

        await Assert.That(lines[0]).IsEqualTo("#EXTM3U");
        await Assert.That(lines[1]).IsEqualTo("#EXTINF:125,Artist One - Title One");
        await Assert.That(lines[2]).IsEqualTo(A);
        await Assert.That(lines[3]).IsEqualTo("#EXTINF:200,Artist Two - Title Two");
        await Assert.That(lines[4]).IsEqualTo(B);
    }
}
