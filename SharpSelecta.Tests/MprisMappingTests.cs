using SharpSelecta.App.Services.Mpris;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class MprisMappingTests
{
    [Test]
    [Arguments(TransportState.NoTrack, false, "Stopped")]
    [Arguments(TransportState.NoTrack, true, "Stopped")]
    [Arguments(TransportState.Finished, false, "Stopped")]
    [Arguments(TransportState.Finished, true, "Stopped")]
    [Arguments(TransportState.Ready, true, "Playing")]
    [Arguments(TransportState.Ready, false, "Paused")]
    public async Task PlaybackStatus_MapsTransportStateAndIsPlaying(TransportState transportState, bool isPlaying, string expected)
    {
        var status = MprisMapping.PlaybackStatus(transportState, isPlaying);

        await Assert.That(status).IsEqualTo(expected);
    }

    [Test]
    public async Task BuildMetadata_WithNoTrack_ReturnsAnEmptyDictionary()
    {
        var metadata = MprisMapping.BuildMetadata(null);

        await Assert.That(metadata.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuildMetadata_WithAFullyTaggedTrack_IncludesAllStandardKeys()
    {
        var track = new Track("/music/song.mp3", "song.mp3")
        {
            Title = "Song Title",
            Artist = "The Artist",
            Album = "The Album",
            Duration = TimeSpan.FromSeconds(200),
        };

        var metadata = MprisMapping.BuildMetadata(track);

        await Assert.That(metadata["xesam:title"]).IsEqualTo("Song Title");
        await Assert.That((string[])metadata["xesam:artist"]).IsEquivalentTo(["The Artist"]);
        await Assert.That(metadata["xesam:album"]).IsEqualTo("The Album");
        await Assert.That(metadata["mpris:length"]).IsEqualTo(200_000_000L);
        await Assert.That(metadata["mpris:trackid"]).IsEqualTo(MprisMapping.TrackId(track));
    }

    [Test]
    public async Task BuildMetadata_WithNoTitle_FallsBackToDisplayName()
    {
        var track = new Track("/music/untagged.mp3", "untagged.mp3");

        var metadata = MprisMapping.BuildMetadata(track);

        await Assert.That(metadata["xesam:title"]).IsEqualTo("untagged.mp3");
    }

    [Test]
    public async Task BuildMetadata_WithNoArtistOrAlbum_OmitsThoseKeys()
    {
        var track = new Track("/music/untagged.mp3", "untagged.mp3");

        var metadata = MprisMapping.BuildMetadata(track);

        await Assert.That(metadata.ContainsKey("xesam:artist")).IsFalse();
        await Assert.That(metadata.ContainsKey("xesam:album")).IsFalse();
    }

    [Test]
    public async Task TrackId_IsStableForTheSameTrack()
    {
        var track = new Track("/music/song.mp3", "song.mp3");

        await Assert.That(MprisMapping.TrackId(track)).IsEqualTo(MprisMapping.TrackId(track));
    }

    [Test]
    public async Task TrackId_DiffersForDifferentFilePaths()
    {
        var a = new Track("/music/a.mp3", "a.mp3");
        var b = new Track("/music/b.mp3", "b.mp3");

        await Assert.That(MprisMapping.TrackId(a)).IsNotEqualTo(MprisMapping.TrackId(b));
    }

    [Test]
    public async Task TrackId_IsAValidObjectPath()
    {
        var track = new Track("/music/song.mp3", "song.mp3");

        var trackId = MprisMapping.TrackId(track);

        await Assert.That(trackId.ToString()).StartsWith("/org/sharpselecta/Track/");
    }
}
