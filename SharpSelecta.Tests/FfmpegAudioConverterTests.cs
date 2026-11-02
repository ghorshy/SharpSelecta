using SharpSelecta.Core.Conversion;
using SharpSelecta.Integrations.Conversion;

namespace SharpSelecta.Tests;

public class FfmpegAudioConverterTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static readonly FfmpegAudioConverter Converter = new();

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private string TempTarget(string extension) => _temp.FilePath($"{Guid.NewGuid():N}{extension}");

    // The fixtures are 8 kHz mono, where LAME caps the bitrate - bitrate checks need a CD-quality source.
    private async Task<string> CreateStereoFlacAsync()
    {
        var path = TempTarget(".flac");
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ffmpeg",
            ["-v", "error", "-f", "lavfi", "-i", "sine=duration=1:sample_rate=44100", "-ac", "2", path]));
        await process!.WaitForExitAsync();
        return path;
    }

    [Test]
    public async Task Probe_Flac_IsLossless()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");

        var info = await Converter.ProbeAsync(FixturePath("untagged-track.flac"), CancellationToken.None);

        await Assert.That(info).IsNotNull();
        await Assert.That(info!.IsLossless).IsTrue();
        await Assert.That(info.Codec).IsEqualTo("flac");
    }

    [Test]
    public async Task Probe_Mp3_IsLossy()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");

        var info = await Converter.ProbeAsync(FixturePath("untagged-track.mp3"), CancellationToken.None);

        await Assert.That(info!.IsLossless).IsFalse();
    }

    [Test]
    public async Task Convert_FlacToAlac_ProducesAnM4aFile()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var flac = FixturePath("untagged-track.flac");
        var probed = (await Converter.ProbeAsync(flac, CancellationToken.None))!;
        var target = TempTarget(".m4a");

        await Converter.ConvertAsync(
            new ConversionRequest(flac, target, ConversionTarget.AlacM4a, null, probed),
            new Progress<double>(), CancellationToken.None);

        var info = await Converter.ProbeAsync(target, CancellationToken.None);
        await Assert.That(info!.Codec).IsEqualTo("alac");
        await Assert.That(info.IsLossless).IsTrue();
    }

    [Test]
    public async Task Convert_FlacToMp3_UsesTheRequestedBitrate()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var source = await CreateStereoFlacAsync();
        var probed = (await Converter.ProbeAsync(source, CancellationToken.None))!;
        var target = TempTarget(".mp3");

        await Converter.ConvertAsync(
            new ConversionRequest(source, target, ConversionTarget.Mp3, 128, probed),
            new Progress<double>(), CancellationToken.None);

        var info = await Converter.ProbeAsync(target, CancellationToken.None);
        await Assert.That(info!.Codec).IsEqualTo("mp3");
        await Assert.That(info.BitrateKbps).IsEqualTo(128);
    }

    [Test]
    public async Task Convert_UsesTheGivenSourceInfoInsteadOfProbingAgain()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var flac = FixturePath("untagged-track.flac");
        var target = TempTarget(".wav");
        var probed = (await Converter.ProbeAsync(flac, CancellationToken.None))!;
        await Assert.That(probed.BitDepth).IsEqualTo(16);

        await Converter.ConvertAsync(
            new ConversionRequest(flac, target, ConversionTarget.Wav, null, probed with { BitDepth = 24 }),
            new Progress<double>(), CancellationToken.None);

        var info = await Converter.ProbeAsync(target, CancellationToken.None);
        await Assert.That(info!.Codec).IsEqualTo("pcm_s24le");
    }
}
