using SharpSelecta.Core.Audio;

namespace SharpSelecta.Tests;

public class ReplayGainTests
{
    private static ReplayGainInfo Info(double? trackGainDb = null, double? trackPeak = null, double? albumGainDb = null, double? albumPeak = null) =>
        new(trackGainDb, trackPeak, albumGainDb, albumPeak);

    [Test]
    public async Task Off_LeavesTheTrackUntouched()
    {
        await Assert.That(Info(trackGainDb: -6.02).LinearGain(VolumeNormalization.Off, untaggedGainDb: 0)).IsEqualTo(1f);
    }

    [Test]
    public async Task Track_AppliesTheTrackGain()
    {
        await Assert.That(Info(trackGainDb: -6.02, albumGainDb: -12.04).LinearGain(VolumeNormalization.Track, untaggedGainDb: 0))
            .IsEqualTo(0.5f).Within(0.001f);
    }

    [Test]
    public async Task Album_AppliesTheAlbumGain()
    {
        await Assert.That(Info(trackGainDb: -6.02, albumGainDb: -12.04).LinearGain(VolumeNormalization.Album, untaggedGainDb: 0))
            .IsEqualTo(0.25f).Within(0.001f);
    }

    [Test]
    public async Task EachMode_FallsBackToTheOtherGain_WhenItsOwnIsMissing()
    {
        await Assert.That(Info(albumGainDb: -6.02).LinearGain(VolumeNormalization.Track, untaggedGainDb: 0)).IsEqualTo(0.5f).Within(0.001f);
        await Assert.That(Info(trackGainDb: -6.02).LinearGain(VolumeNormalization.Album, untaggedGainDb: 0)).IsEqualTo(0.5f).Within(0.001f);
    }

    [Test]
    public async Task AnUntaggedTrack_GetsTheUntaggedGain()
    {
        await Assert.That(Info().LinearGain(VolumeNormalization.Track, untaggedGainDb: -12.04)).IsEqualTo(0.25f).Within(0.001f);
        await Assert.That(Info().LinearGain(VolumeNormalization.Off, untaggedGainDb: -12.04)).IsEqualTo(1f);
    }

    [Test]
    public async Task APositiveGain_IsCappedSoThePeakDoesNotClip()
    {
        await Assert.That(Info(trackGainDb: 6.02, trackPeak: 0.8).LinearGain(VolumeNormalization.Track, untaggedGainDb: 0)).IsEqualTo(1.25f).Within(0.001f);
        await Assert.That(Info(albumGainDb: 6.02, albumPeak: 0.8, trackPeak: 0.4).LinearGain(VolumeNormalization.Album, untaggedGainDb: 0)).IsEqualTo(1.25f).Within(0.001f);
    }

    [Test]
    public async Task AFallbackGain_IsCappedByItsOwnPeak()
    {
        await Assert.That(Info(trackPeak: 0.4, albumGainDb: 6.02, albumPeak: 0.8).LinearGain(VolumeNormalization.Track, untaggedGainDb: 0))
            .IsEqualTo(1.25f).Within(0.001f);
    }

    [Test]
    public async Task FromTags_ReadsTheFourValues_WhateverTheKeyCase()
    {
        var info = ReplayGainInfo.FromTags(new Dictionary<string, string>
        {
            ["REPLAYGAIN_TRACK_GAIN"] = "-6.50 dB",
            ["REPLAYGAIN_TRACK_PEAK"] = "0.95",
            ["replaygain_album_gain"] = "+3.10 dB",
            ["replaygain_album_peak"] = "0.99",
            ["TITLE"] = "Song",
        });

        await Assert.That(info).IsEqualTo(Info(-6.5, 0.95, 3.1, 0.99));
    }

    [Test]
    public async Task FromTags_IgnoresValuesItCannotRead()
    {
        var info = ReplayGainInfo.FromTags(new Dictionary<string, string>
        {
            ["REPLAYGAIN_TRACK_GAIN"] = "loud",
            ["REPLAYGAIN_TRACK_PEAK"] = "",
            ["REPLAYGAIN_ALBUM_GAIN"] = "NaN dB",
            ["REPLAYGAIN_ALBUM_PEAK"] = "Infinity",
        });

        await Assert.That(info).IsEqualTo(Info());
    }

    // Written the way taggers store them: Vorbis comments, ID3 TXXX frames, MP4 iTunes freeform atoms.
    [Test]
    [Arguments("untagged-track.flac", "REPLAYGAIN_TRACK_GAIN", "REPLAYGAIN_ALBUM_GAIN")]
    [Arguments("untagged-track.ogg", "REPLAYGAIN_TRACK_GAIN", "REPLAYGAIN_ALBUM_GAIN")]
    [Arguments("untagged-track.mp3", "REPLAYGAIN_TRACK_GAIN", "REPLAYGAIN_ALBUM_GAIN")]
    [Arguments("untagged-track.m4a", "----:com.apple.iTunes:replaygain_track_gain", "----:com.apple.iTunes:replaygain_album_gain")]
    public async Task Read_FindsTheTagsInEachFormat(string fixtureName, string trackGainKey, string albumGainKey)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, fixtureName);
        File.Copy(TestPaths.Fixture(fixtureName), path);
        var track = new ATL.Track(path);
        track.AdditionalFields[trackGainKey] = "-6.50 dB";
        track.AdditionalFields[albumGainKey] = "-7.10 dB";
        track.Save();

        var info = ReplayGainInfo.Read(path);

        await Assert.That(info.TrackGainDb).IsEqualTo(-6.5);
        await Assert.That(info.AlbumGainDb).IsEqualTo(-7.1);
    }

    [Test]
    public async Task Read_OfAnUntaggedOrMissingFile_IsNone()
    {
        await Assert.That(ReplayGainInfo.Read(TestPaths.Fixture("untagged-track.flac"))).IsEqualTo(ReplayGainInfo.None);
        await Assert.That(ReplayGainInfo.Read(TestPaths.Fixture("does-not-exist.flac"))).IsEqualTo(ReplayGainInfo.None);
    }
}
