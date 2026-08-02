using SharpSelecta.Core.Audio;

namespace SharpSelecta.Tests;

public class VolumeScaleTests
{
    [Test]
    [Arguments(0.0)]
    [Arguments(0.4)]
    [Arguments(1.0)]
    public async Task ToAmplitude_WithLinearCurve_ReturnsTheSliderPositionUnchanged(double sliderPosition)
    {
        var amplitude = VolumeScale.ToAmplitude(sliderPosition, VolumeCurve.Linear);

        await Assert.That(amplitude).IsEqualTo(sliderPosition);
    }

    [Test]
    public async Task ToAmplitude_WithLogarithmicCurve_SquaresTheSliderPosition()
    {
        var amplitude = VolumeScale.ToAmplitude(0.5, VolumeCurve.Logarithmic);

        await Assert.That(amplitude).IsEqualTo(0.25);
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(1.0)]
    public async Task ToAmplitude_WithLogarithmicCurve_LeavesTheEndpointsUnchanged(double sliderPosition)
    {
        var amplitude = VolumeScale.ToAmplitude(sliderPosition, VolumeCurve.Logarithmic);

        await Assert.That(amplitude).IsEqualTo(sliderPosition);
    }

    [Test]
    public async Task ToAmplitude_ClampsOutOfRangeInput()
    {
        await Assert.That(VolumeScale.ToAmplitude(-0.5, VolumeCurve.Linear)).IsEqualTo(0.0);
        await Assert.That(VolumeScale.ToAmplitude(1.5, VolumeCurve.Linear)).IsEqualTo(1.0);
    }
}
