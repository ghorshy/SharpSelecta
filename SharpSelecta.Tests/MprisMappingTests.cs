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

    [Test]
    public async Task CanPlay_WhenAbleToResumeAnAlreadyLoadedTrack_IsTrue()
    {
        await Assert.That(MprisMapping.CanPlay(canResumeOrPause: true, hasCurrentTrack: true, canGoNext: false)).IsTrue();
    }

    // The regression this guards against: tracks queued (e.g. via "Add to Queue") but nothing
    // played yet this session - CanGoNext is true (the queue has something), but there's no
    // current track to resume. playerctl skips a player reporting CanPlay=false for Play/PlayPause
    // and falls through to a different one entirely, even though Next/Previous still worked fine.
    [Test]
    public async Task CanPlay_WithNoCurrentTrackButSomethingQueuedToAdvanceTo_IsTrue()
    {
        await Assert.That(MprisMapping.CanPlay(canResumeOrPause: false, hasCurrentTrack: false, canGoNext: true)).IsTrue();
    }

    [Test]
    public async Task CanPlay_WithNoCurrentTrackAndNothingQueued_IsFalse()
    {
        await Assert.That(MprisMapping.CanPlay(canResumeOrPause: false, hasCurrentTrack: false, canGoNext: false)).IsFalse();
    }

    [Test]
    public async Task CanPlay_WithACurrentTrackThatCannotBeResumed_IsFalse()
    {
        // e.g. TransportState.Finished with repeat off and nothing left queued - PlayPauseCommand's
        // own CanExecute is false, and there's nothing left for Next to advance to either.
        await Assert.That(MprisMapping.CanPlay(canResumeOrPause: false, hasCurrentTrack: true, canGoNext: false)).IsFalse();
    }
}
