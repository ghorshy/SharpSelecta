using SharpSelecta.Core.Conversion;

namespace SharpSelecta.Tests;

public class ConversionRulesTests
{
    private static SourceInfo Lossless(string codec = "flac") => new(codec, true, null, 44100, 16, 2, TimeSpan.FromSeconds(10));

    private static SourceInfo Lossy(int? bitrate, string codec = "mp3") => new(codec, false, bitrate, 44100, null, 2, TimeSpan.FromSeconds(10));

    [Test]
    public async Task Check_LossySourceToLosslessTarget_IsBlocked()
    {
        await Assert.That(ConversionRules.Check(Lossy(320), ConversionTarget.Flac)).IsEqualTo(ConversionBlock.LossyToLossless);
        await Assert.That(ConversionRules.Check(Lossy(320, "aac"), ConversionTarget.AlacM4a)).IsEqualTo(ConversionBlock.LossyToLossless);
        await Assert.That(ConversionRules.Check(Lossy(320), ConversionTarget.Wav)).IsEqualTo(ConversionBlock.LossyToLossless);
    }

    [Test]
    public async Task Check_LosslessSourceToItsOwnFormat_IsBlocked()
    {
        await Assert.That(ConversionRules.Check(Lossless("flac"), ConversionTarget.Flac)).IsEqualTo(ConversionBlock.SameFormat);
        await Assert.That(ConversionRules.Check(Lossless("alac"), ConversionTarget.AlacM4a)).IsEqualTo(ConversionBlock.SameFormat);
        await Assert.That(ConversionRules.Check(Lossless("pcm_s16le"), ConversionTarget.Wav)).IsEqualTo(ConversionBlock.SameFormat);
    }

    [Test]
    public async Task Check_FlacToAlac_IsAllowed()
    {
        await Assert.That(ConversionRules.Check(Lossless("flac"), ConversionTarget.AlacM4a)).IsNull();
    }

    [Test]
    public async Task Check_LossySourceWithNoBitrateAtOrBelowTheLowestOffer_IsBlocked()
    {
        await Assert.That(ConversionRules.Check(Lossy(32), ConversionTarget.Mp3)).IsEqualTo(ConversionBlock.NoLowerBitrate);
        await Assert.That(ConversionRules.Check(Lossy(null), ConversionTarget.Mp3)).IsEqualTo(ConversionBlock.NoLowerBitrate);
    }

    [Test]
    public async Task AllowedBitrates_ForLossySource_NeverExceedTheSourceBitrate()
    {
        var allowed = ConversionRules.AllowedBitrates(Lossy(160), ConversionTarget.Mp3);

        await Assert.That(allowed).IsEquivalentTo([160, 128, 96, 64]);
    }

    [Test]
    public async Task AllowedBitrates_ForLosslessSource_OffersEverything()
    {
        var allowed = ConversionRules.AllowedBitrates(Lossless(), ConversionTarget.AacM4a);

        await Assert.That(allowed).IsEquivalentTo([320, 256, 224, 192, 160, 128, 96, 64]);
    }

    [Test]
    public async Task AllowedBitrates_ForLosslessTargets_IsEmpty()
    {
        await Assert.That(ConversionRules.AllowedBitrates(Lossless(), ConversionTarget.Flac)).IsEmpty();
    }

    [Test]
    public async Task KeepsAllTags_IsFalseOnlyForWav()
    {
        await Assert.That(ConversionRules.KeepsAllTags(ConversionTarget.Wav)).IsFalse();
        await Assert.That(ConversionRules.KeepsAllTags(ConversionTarget.Mp3)).IsTrue();
        await Assert.That(ConversionRules.KeepsAllTags(ConversionTarget.AlacM4a)).IsTrue();
    }

    [Test]
    public async Task Check_WavAndAiff_AreDifferentFormatsOfTheSameKind()
    {
        await Assert.That(ConversionRules.Check(Lossless("pcm_s16le"), ConversionTarget.Aiff)).IsNull();
        await Assert.That(ConversionRules.Check(Lossless("pcm_s16be"), ConversionTarget.Wav)).IsNull();
        await Assert.That(ConversionRules.Check(Lossless("pcm_s24be"), ConversionTarget.Aiff)).IsEqualTo(ConversionBlock.SameFormat);
    }

    [Test]
    public async Task Check_OggVorbis_IsLossy()
    {
        await Assert.That(ConversionRules.Check(Lossy(192, "vorbis"), ConversionTarget.Aiff)).IsEqualTo(ConversionBlock.LossyToLossless);
        await Assert.That(ConversionRules.AllowedBitrates(Lossless(), ConversionTarget.OggVorbis)).IsEquivalentTo([320, 256, 224, 192, 160, 128, 96, 64]);
        await Assert.That(ConversionRules.Check(Lossy(192, "vorbis"), ConversionTarget.OggVorbis)).IsNull();
    }

    [Test]
    public async Task Extension_OfOggAndAiff()
    {
        await Assert.That(ConversionRules.Extension(ConversionTarget.OggVorbis)).IsEqualTo(".ogg");
        await Assert.That(ConversionRules.Extension(ConversionTarget.Aiff)).IsEqualTo(".aiff");
    }
}
