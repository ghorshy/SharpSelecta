using SharpSelecta.App.Views;

namespace SharpSelecta.Tests;

public class WaveformSliderViewTests
{
    [Test]
    public async Task Downsample_WithFewerTargetBucketsThanSource_TakesMaxAbsPerBucket()
    {
        float[] source = [0.1f, 0.9f, -0.2f, 0.3f, -0.8f, 0.1f];

        var result = WaveformSliderView.Downsample(source, 3);

        await Assert.That(result).IsEquivalentTo([0.9f, 0.3f, 0.8f]);
    }

    [Test]
    public async Task Downsample_EmptySource_ReturnsEmpty()
    {
        var result = WaveformSliderView.Downsample([], 100);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Downsample_TargetCountZeroOrNegative_ReturnsEmpty()
    {
        var result = WaveformSliderView.Downsample([0.1f, 0.2f], 0);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Downsample_ResultLengthAlwaysMatchesTargetCount()
    {
        var source = Enumerable.Range(0, 2000).Select(i => (float)(i % 10) / 10f).ToArray();

        var result = WaveformSliderView.Downsample(source, 137);

        await Assert.That(result.Length).IsEqualTo(137);
    }

    [Test]
    public async Task Downsample_TargetCountEqualToSourceCount_ReturnsOnePerBucket()
    {
        float[] source = [0.2f, -0.4f, 0.6f];

        var result = WaveformSliderView.Downsample(source, 3);

        await Assert.That(result).IsEquivalentTo([0.2f, 0.4f, 0.6f]);
    }

    [Test]
    public async Task Downsample_TargetCountLargerThanSourceCount_DegradesGracefullyWithoutCrashing()
    {
        float[] source = [0.5f, -0.7f];

        var result = WaveformSliderView.Downsample(source, 10);

        await Assert.That(result.Length).IsEqualTo(10);
    }
}
