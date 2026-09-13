using SharpSelecta.App.ViewModels;

namespace SharpSelecta.Tests;

public class EqualizerBandViewModelTests
{
    [Test]
    public async Task GainDbLabel_FormatsPositiveGainWithExplicitSign()
    {
        var band = new EqualizerBandViewModel(0, "62 Hz", 3.0);

        await Assert.That(band.GainDbLabel).IsEqualTo("+3.0 dB");
    }

    [Test]
    public async Task GainDbLabel_FormatsNegativeGain()
    {
        var band = new EqualizerBandViewModel(0, "62 Hz", -1.5);

        await Assert.That(band.GainDbLabel).IsEqualTo("-1.5 dB");
    }

    [Test]
    public async Task GainDbLabel_FormatsZeroWithoutASign()
    {
        var band = new EqualizerBandViewModel(0, "62 Hz", 0.0);

        await Assert.That(band.GainDbLabel).IsEqualTo("0.0 dB");
    }

    [Test]
    public async Task GainDbLabel_UpdatesWhenGainDbChanges()
    {
        var band = new EqualizerBandViewModel(0, "62 Hz", 0.0);

        band.GainDb = 6.25;

        await Assert.That(band.GainDbLabel).IsEqualTo("+6.3 dB");
    }
}
