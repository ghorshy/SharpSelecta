using SharpSelecta.Integrations.Conversion;

namespace SharpSelecta.Tests;

public class FfprobeOutputTests
{
    [Test]
    public async Task ReadsTheAudioStream()
    {
        var info = FfprobeOutput.ToSourceInfo("""
            { "streams": [ { "index": 0, "codec_type": "audio", "codec_name": "FLAC", "sample_rate": "96000", "channels": 2,
                             "bits_per_raw_sample": "24", "bit_rate": "2400000", "duration": "183.500000" } ],
              "format": { "duration": "183.500000", "bit_rate": "2410000" } }
            """);

        await Assert.That(info).IsNotNull();
        await Assert.That(info!.Codec).IsEqualTo("flac");
        await Assert.That(info.IsLossless).IsTrue();
        await Assert.That(info.SampleRate).IsEqualTo(96000);
        await Assert.That(info.Channels).IsEqualTo(2);
        await Assert.That(info.BitDepth).IsEqualTo(24);
        await Assert.That(info.BitrateKbps).IsEqualTo(2400);
        await Assert.That(info.Duration).IsEqualTo(TimeSpan.FromSeconds(183.5));
    }

    [Test]
    public async Task WithoutAStreamBitrate_UsesTheFormats()
    {
        var info = FfprobeOutput.ToSourceInfo("""
            { "streams": [ { "index": 0, "codec_type": "audio", "codec_name": "opus", "sample_rate": "48000", "channels": 2 } ],
              "format": { "duration": "2.0", "bit_rate": "113000" } }
            """);

        await Assert.That(info!.BitrateKbps).IsEqualTo(113);
        await Assert.That(info.BitDepth).IsNull();
    }

    [Test]
    public async Task WithoutAnAudioStream_IsNull()
    {
        var info = FfprobeOutput.ToSourceInfo("""
            { "streams": [ { "index": 0, "codec_type": "video", "codec_name": "mjpeg" } ], "format": { "duration": "1.0" } }
            """);

        await Assert.That(info).IsNull();
    }

    [Test]
    public async Task PrefersTheDefaultAudioStream()
    {
        var info = FfprobeOutput.ToSourceInfo("""
            { "streams": [
                { "index": 0, "codec_type": "audio", "codec_name": "aac", "sample_rate": "44100", "channels": 2, "disposition": { "default": 0 } },
                { "index": 1, "codec_type": "audio", "codec_name": "alac", "sample_rate": "48000", "channels": 2, "disposition": { "default": 1 } } ] }
            """);

        await Assert.That(info!.Codec).IsEqualTo("alac");
    }

    [Test]
    public async Task TakesTheLongestDuration_IncludingATaggedOne()
    {
        var info = FfprobeOutput.ToSourceInfo("""
            { "streams": [ { "index": 0, "codec_type": "audio", "codec_name": "opus", "sample_rate": "48000", "channels": 2,
                             "tags": { "DURATION": "00:03:02.008000000" } } ],
              "format": { "duration": "182.000000" } }
            """);

        await Assert.That(info!.Duration).IsEqualTo(TimeSpan.FromSeconds(182.008));
    }

    [Test]
    public async Task FallsBackToBitsPerSample_AndTreatsZeroAsUnknown()
    {
        const string Template = """
            { "streams": [ { "index": 0, "codec_type": "audio", "codec_name": "pcm_s24le", "sample_rate": "44100", "channels": 2,
                             "bits_per_sample": BITS } ] }
            """;

        await Assert.That(FfprobeOutput.ToSourceInfo(Template.Replace("BITS", "24"))!.BitDepth).IsEqualTo(24);
        await Assert.That(FfprobeOutput.ToSourceInfo(Template.Replace("BITS", "0"))!.BitDepth).IsNull();
    }

    [Test]
    [Arguments("")]
    [Arguments("not json")]
    [Arguments("""{ "streams": [ { "codec_type": "audio", "codec_name": "mp3", "sample_rate": "N/A", "channels": 2 } ] }""")]
    public async Task UnreadableOutput_IsNull(string json)
    {
        await Assert.That(FfprobeOutput.ToSourceInfo(json)).IsNull();
    }
}
