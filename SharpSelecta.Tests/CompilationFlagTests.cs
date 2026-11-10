using ATL;
using SharpSelecta.Core.Library;
using AtlTrack = ATL.Track;

namespace SharpSelecta.Tests;

public class CompilationFlagTests
{
    private static string CopyFixture(string fixtureName, out DirectoryInfo dir)
    {
        dir = Directory.CreateTempSubdirectory("sharpselecta-compilation-flag-tests-");
        var path = Path.Combine(dir.FullName, fixtureName);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName), path);
        return path;
    }

    [Test]
    [Arguments("untagged-track.mp3")]
    [Arguments("untagged-track.m4a")]
    [Arguments("untagged-track-alac.m4a")]
    [Arguments("untagged-track.flac")]
    [Arguments("untagged-track.ogg")]
    [Arguments("untagged-track.wav")]
    [Arguments("untagged-track.aiff")]
    public async Task WriteCompilationFlag_SetsAndClearsTheFlag(string fixtureName)
    {
        var path = CopyFixture(fixtureName, out var dir);
        try
        {
            await Assert.That(CompilationFlag.IsSet(path)).IsFalse();

            TrackTagEditor.WriteCompilationFlag(path, true);
            await Assert.That(CompilationFlag.IsSet(path)).IsTrue();

            TrackTagEditor.WriteCompilationFlag(path, false);
            await Assert.That(CompilationFlag.IsSet(path)).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task WriteCompilationFlag_KeepsEveryOtherTag()
    {
        var path = CopyFixture("untagged-track.m4a", out var dir);
        try
        {
            var cover = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "cover-red.jpg"));
            var tagged = new AtlTrack(path) { Title = "It's a Sin", AlbumArtist = "Pet Shop Boys", EncodedBy = "Lavf61.7.100" };
            tagged.AdditionalFields["----:com.apple.iTunes:MusicBrainz Album Id"] = "a1b2";
            tagged.AdditionalFields["cpil"] = "1";
            tagged.EmbeddedPictures.Add(PictureInfo.fromBinaryData(cover, PictureInfo.PIC_TYPE.Front));
            tagged.Save();

            TrackTagEditor.WriteCompilationFlag(path, false);

            var after = new AtlTrack(path);
            await Assert.That(CompilationFlag.IsSet(path)).IsFalse();
            await Assert.That(after.Title).IsEqualTo("It's a Sin");
            await Assert.That(after.AlbumArtist).IsEqualTo("Pet Shop Boys");
            await Assert.That(after.EncodedBy).IsEqualTo("Lavf61.7.100");
            await Assert.That(after.AdditionalFields.Values).Contains("a1b2");
            await Assert.That(after.EmbeddedPictures.Count).IsEqualTo(1);
            await Assert.That(after.EmbeddedPictures[0].PictureData).IsEquivalentTo(cover);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
