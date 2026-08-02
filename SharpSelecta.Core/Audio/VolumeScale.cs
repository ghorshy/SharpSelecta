namespace SharpSelecta.Core.Audio;

public static class VolumeScale
{
    // Human loudness perception is roughly logarithmic, so a plain linear slider spends most of
    // its travel in the "too loud" range and squeezes all the useful low-volume adjustment into a
    // sliver near zero. Squaring the linear slider position is a common, cheap approximation of an
    // audio-taper (logarithmic) potentiometer - unlike a true dB-based curve, it needs no arbitrary
    // "silence floor" decision (a dB formula's gain never actually reaches zero), since 0 squared is
    // still exactly 0.
    public static double ToAmplitude(double sliderPosition, VolumeCurve curve)
    {
        var clamped = Math.Clamp(sliderPosition, 0.0, 1.0);
        return curve == VolumeCurve.Logarithmic ? clamped * clamped : clamped;
    }
}
