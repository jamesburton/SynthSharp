using SynthSharp.Core.Audio;

namespace SynthSharp.Audio;

public static class WaveformSampleGenerator
{
    public static double NextSample(WaveformType waveform, double phaseRadians)
    {
        var normalizedPhase = phaseRadians / (2d * Math.PI);
        normalizedPhase -= Math.Floor(normalizedPhase);

        return waveform switch
        {
            WaveformType.Sine => Math.Sin(phaseRadians),
            WaveformType.Square => normalizedPhase < 0.5 ? 1d : -1d,
            WaveformType.Sawtooth => (2d * normalizedPhase) - 1d,
            WaveformType.Triangle => 1d - (4d * Math.Abs(normalizedPhase - 0.5d)),
            _ => 0d,
        };
    }
}
