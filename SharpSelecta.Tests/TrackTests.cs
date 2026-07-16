using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class TrackTests
{
    [Test]
    [Arguments(44100, "44.1 kHz")]
    [Arguments(48000, "48 kHz")]
    [Arguments(96000, "96 kHz")]
    [Arguments(22050, "22.05 kHz")]
    [Arguments(0, "")]
    [Arguments(-1, "")]
    public async Task SampleRateDisplay_FormatsHertzAsKiloHertz(int sampleRate, string expected)
    {
        var track = new Track("/music/a.mp3", "a.mp3") { SampleRate = sampleRate };

        await Assert.That(track.SampleRateDisplay).IsEqualTo(expected);
    }

    [Test]
    [Arguments(320, "320 kbps")]
    [Arguments(128, "128 kbps")]
    [Arguments(0, "")]
    [Arguments(-1, "")]
    public async Task BitrateDisplay_AppendsKbpsUnit(int bitrate, string expected)
    {
        var track = new Track("/music/a.mp3", "a.mp3") { Bitrate = bitrate };

        await Assert.That(track.BitrateDisplay).IsEqualTo(expected);
    }

    [Test]
    [Arguments(16, "16 Bit")]
    [Arguments(24, "24 Bit")]
    [Arguments(0, "")]
    [Arguments(-1, "")]
    public async Task BitDepthDisplay_AppendsBitUnit_OrBlankWhenNotApplicable(int bitDepth, string expected)
    {
        var track = new Track("/music/a.mp3", "a.mp3") { BitDepth = bitDepth };

        await Assert.That(track.BitDepthDisplay).IsEqualTo(expected);
    }

    [Test]
    public async Task LengthDisplay_UnderAnHour_OmitsHours()
    {
        var track = new Track("/music/a.mp3", "a.mp3") { Duration = TimeSpan.FromSeconds(185) };

        await Assert.That(track.LengthDisplay).IsEqualTo("3:05");
    }

    [Test]
    public async Task LengthDisplay_AnHourOrLonger_IncludesHours()
    {
        var track = new Track("/music/a.mp3", "a.mp3") { Duration = TimeSpan.FromSeconds(3725) };

        await Assert.That(track.LengthDisplay).IsEqualTo("1:02:05");
    }
}
