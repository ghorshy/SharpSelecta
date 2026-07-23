using SharpSelecta.App.Formatting;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class TrackFormattingTests
{
    [Test]
    [Arguments(44100, "44.1 kHz")]
    [Arguments(48000, "48 kHz")]
    [Arguments(96000, "96 kHz")]
    [Arguments(22050, "22.05 kHz")]
    [Arguments(0, "")]
    [Arguments(-1, "")]
    public async Task FormatSampleRate_FormatsHertzAsKiloHertz(int sampleRate, string expected)
    {
        await Assert.That(TrackFormatting.FormatSampleRate(sampleRate)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(320, "320 kbps")]
    [Arguments(128, "128 kbps")]
    [Arguments(0, "")]
    [Arguments(-1, "")]
    public async Task FormatBitrate_AppendsKbpsUnit(int bitrate, string expected)
    {
        await Assert.That(TrackFormatting.FormatBitrate(bitrate)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(16, "16 Bit")]
    [Arguments(24, "24 Bit")]
    [Arguments(0, "")]
    [Arguments(-1, "")]
    public async Task FormatBitDepth_AppendsBitUnit_OrBlankWhenNotApplicable(int bitDepth, string expected)
    {
        await Assert.That(TrackFormatting.FormatBitDepth(bitDepth)).IsEqualTo(expected);
    }

    [Test]
    public async Task FormatDuration_UnderAnHour_OmitsHours()
    {
        await Assert.That(TrackFormatting.FormatDuration(TimeSpan.FromSeconds(185))).IsEqualTo("3:05");
    }

    [Test]
    public async Task FormatDuration_AnHourOrLonger_IncludesHours()
    {
        await Assert.That(TrackFormatting.FormatDuration(TimeSpan.FromSeconds(3725))).IsEqualTo("1:02:05");
    }

    [Test]
    public async Task TechnicalSummary_CombinesFileTypeSampleRateBitrateAndLength()
    {
        var track = new Track("/music/a.mp3", "a.mp3")
        {
            FileType = "MP3",
            SampleRate = 44100,
            Bitrate = 320,
            Duration = TimeSpan.FromSeconds(185),
        };

        await Assert.That(TrackFormatting.TechnicalSummary(track)).IsEqualTo("MP3 44.1 kHz, 320 kbps, 3:05");
    }
}
