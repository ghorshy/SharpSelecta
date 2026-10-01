using SharpSelecta.Core.Library;
using AtlTrack = ATL.Track;

namespace SharpSelecta.Tests;

public class TrackCreditsTests
{
    private static readonly TrackTagEdits SomeEdits = new("Title", "Artist", null, "Album", null, null, 2001, 3);
    private static readonly TrackCredits FullCredits = new("Rem1;Rem2", "Comp1;Comp2", "Cond", "Lyr1;Lyr2");

    private static string Copy(string fixture, out DirectoryInfo dir)
    {
        dir = Directory.CreateTempSubdirectory("sharpselecta-credits-tests-");
        var path = Path.Combine(dir.FullName, fixture);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), path);
        return path;
    }

    [Test]
    [Arguments("untagged-track.mp3")]
    [Arguments("untagged-track.flac")]
    [Arguments("untagged-track.m4a")]
    [Arguments("untagged-track.wav")]
    public async Task Write_RoundTripsEveryCredit(string fixture)
    {
        var path = Copy(fixture, out var dir);
        try
        {
            TrackTagEditor.Write(path, SomeEdits, credits: FullCredits);

            await Assert.That(TrackCredits.Read(path)).IsEqualTo(FullCredits);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    [Arguments("untagged-track.mp3")]
    [Arguments("untagged-track.flac")]
    [Arguments("untagged-track.m4a")]
    [Arguments("untagged-track.wav")]
    public async Task Write_WithEmptyCredits_ClearsThemAll(string fixture)
    {
        var path = Copy(fixture, out var dir);
        try
        {
            TrackTagEditor.Write(path, SomeEdits, credits: FullCredits);

            TrackTagEditor.Write(path, SomeEdits, credits: TrackCredits.None);

            await Assert.That(TrackCredits.Read(path)).IsEqualTo(TrackCredits.None);
            var leftovers = new AtlTrack(path).AdditionalFields.Keys
                .Where(k => k.Equals("REMIXER", StringComparison.OrdinalIgnoreCase) || k.Equals("TPE4", StringComparison.OrdinalIgnoreCase));
            await Assert.That(leftovers).IsEmpty();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    [Arguments("untagged-track.mp3", "TPE4")]
    [Arguments("untagged-track.wav", "TPE4")]
    [Arguments("untagged-track.flac", "REMIXER")]
    [Arguments("untagged-track.m4a", "REMIXER")]
    public async Task Remixer_IsWrittenUnderTheNameTheFormatUses(string fixture, string expectedKey)
    {
        var path = Copy(fixture, out var dir);
        try
        {
            TrackTagEditor.Write(path, SomeEdits, credits: new TrackCredits("Someone", null, null, null));

            var keys = new AtlTrack(path).AdditionalFields.Keys.Where(k => k is "TPE4" or "REMIXER" || k.Equals("remixer", StringComparison.OrdinalIgnoreCase)).ToList();
            await Assert.That(keys).IsEquivalentTo([expectedKey]);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Write_WithoutCredits_LeavesTheFilesCreditsAlone()
    {
        var path = Copy("untagged-track.mp3", out var dir);
        try
        {
            TrackTagEditor.Write(path, SomeEdits, credits: FullCredits);

            TrackTagEditor.Write(path, SomeEdits with { Title = "Other" });

            await Assert.That(TrackCredits.Read(path)).IsEqualTo(FullCredits);
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(path)!.Title).IsEqualTo("Other");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    [Arguments("untagged-track.flac", "TPE4")]
    [Arguments("untagged-track.mp3", "REMIXER")]
    public async Task Read_AlsoFindsARemixerStoredUnderTheOtherFormatsName(string fixture, string foreignKey)
    {
        var path = Copy(fixture, out var dir);
        try
        {
            var atl = new AtlTrack(path);
            atl.AdditionalFields[foreignKey] = "Found Me";
            atl.Save();

            await Assert.That(TrackCredits.Read(path).Remixer).IsEqualTo("Found Me");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Read_OnAnUnreadableFile_ReturnsNoCredits()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-credits-tests-");
        try
        {
            await Assert.That(TrackCredits.Read(Path.Combine(dir.FullName, "missing.mp3"))).IsEqualTo(TrackCredits.None);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
