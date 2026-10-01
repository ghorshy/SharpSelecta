using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class ArtistListTests
{
    [Test]
    public async Task Split_BreaksTheDisplayStringIntoTrimmedArtists()
    {
        await Assert.That(string.Join("|", ArtistList.Split("Daft Punk; Pharrell Williams ;Nile Rodgers")))
            .IsEqualTo("Daft Punk|Pharrell Williams|Nile Rodgers");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(" ; ;")]
    public async Task Split_WithNoArtists_ReturnsEmpty(string? value)
    {
        await Assert.That(ArtistList.Split(value)).IsEmpty();
    }

    [Test]
    public async Task Split_DropsCaseInsensitiveDuplicates_KeepingTheFirstSpelling()
    {
        await Assert.That(ArtistList.Split("Moby;moby;Air")).IsEquivalentTo(["Moby", "Air"]);
    }

    [Test]
    public async Task Join_NormalizesAndJoinsInOrder()
    {
        await Assert.That(ArtistList.Join([" A ", "B", "a", ""])).IsEqualTo("A;B");
    }

    [Test]
    public async Task Join_SplitsNamesThatContainTheSeparator()
    {
        await Assert.That(ArtistList.Join(["A;B", "C"])).IsEqualTo("A;B;C");
    }

    [Test]
    public async Task Join_OfNothing_IsNull()
    {
        await Assert.That(ArtistList.Join([])).IsNull();
        await Assert.That(ArtistList.Join(["  "])).IsNull();
    }

    [Test]
    [Arguments("untagged-track.mp3")]
    [Arguments("untagged-track.flac")]
    [Arguments("untagged-track.m4a")]
    [Arguments("untagged-track.wav")]
    public async Task MultipleArtists_RoundTripThroughEveryFormat(string fixtureName)
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-artist-list-tests-");
        try
        {
            var path = Path.Combine(dir.FullName, fixtureName);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName), path);

            TrackTagEditor.Write(path, new TrackTagEdits("T", ArtistList.Join(["First", "Second", "Third"]), null, null, null, null, null, null));

            var artists = ArtistList.Split(MusicLibraryScanner.ReadTrackIfExists(path)!.Artist);
            await Assert.That(string.Join("|", artists)).IsEqualTo("First|Second|Third");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
