namespace SharpSelecta.Core.Audio;

public static class VolumeScale
{
    public static double ToAmplitude(double sliderPosition, VolumeCurve curve)
    {
        var clamped = Math.Clamp(sliderPosition, 0.0, 1.0);
        return curve == VolumeCurve.Logarithmic ? clamped * clamped : clamped;
    }
}
