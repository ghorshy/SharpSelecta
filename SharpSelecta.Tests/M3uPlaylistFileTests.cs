using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class M3uPlaylistFileTests
{
    [Test]
    public async Task ParsePaths_SkipsCommentsAndBlankLines()
    {
        const string content = "#EXTM3U\n#EXTINF:200,Artist - Title\n/music/a.mp3\n\n/music/b.mp3\n";

        var paths = M3uPlaylistFile.ParsePaths(content, "/music");

        await Assert.That(paths).IsEquivalentTo(["/music/a.mp3", "/music/b.mp3"]);
    }

    [Test]
    public async Task ParsePaths_ResolvesRelativePathsAgainstTheBaseDirectory()
    {
        const string content = "songs/track.mp3";

        var paths = M3uPlaylistFile.ParsePaths(content, "/home/user/Music");

        await Assert.That(paths).IsEquivalentTo([Path.Combine("/home/user/Music", "songs", "track.mp3")]);
    }

    [Test]
    public async Task ParsePaths_LeavesAbsolutePathsUnchanged()
    {
        const string content = "/absolute/path/track.mp3";

        var paths = M3uPlaylistFile.ParsePaths(content, "/home/user/Music");

        await Assert.That(paths).IsEquivalentTo(["/absolute/path/track.mp3"]);
    }

    [Test]
    public async Task ParsePaths_HandlesCrlfLineEndings()
    {
        const string content = "/music/a.mp3\r\n/music/b.mp3\r\n";

        var paths = M3uPlaylistFile.ParsePaths(content, "/music");

        await Assert.That(paths).IsEquivalentTo(["/music/a.mp3", "/music/b.mp3"]);
    }

    [Test]
    public async Task Write_ProducesAValidM3u8WithExtinfLinesInOrder()
    {
        var tracks = new[]
        {
            ("/music/a.mp3", TimeSpan.FromSeconds(125), "Artist One", "Title One"),
            ("/music/b.mp3", TimeSpan.FromSeconds(200), "Artist Two", "Title Two"),
        };

        var content = M3uPlaylistFile.Write(tracks);
        var lines = content.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();

        await Assert.That(lines[0]).IsEqualTo("#EXTM3U");
        await Assert.That(lines[1]).IsEqualTo("#EXTINF:125,Artist One - Title One");
        await Assert.That(lines[2]).IsEqualTo("/music/a.mp3");
        await Assert.That(lines[3]).IsEqualTo("#EXTINF:200,Artist Two - Title Two");
        await Assert.That(lines[4]).IsEqualTo("/music/b.mp3");
    }
}
