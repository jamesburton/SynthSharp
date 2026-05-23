using SynthSharp.Core.Audio;

namespace SynthSharp.Audio;

/// <summary>Generates a single PCM sample for the requested waveform at the requested phase.</summary>
public static class WaveformSampleGenerator
{
    /// <summary>Returns the waveform's sample value in [-1.0, 1.0] for the given phase.</summary>
    /// <param name="waveform">Which waveform to evaluate.</param>
    /// <param name="phaseRadians">Phase position in radians. Ignored for <see cref="WaveformType.Noise"/>.</param>
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

            // White noise: phase-independent uniform random sample in [-1, 1].
            // Random.Shared is thread-safe in .NET 6+.
            WaveformType.Noise => (Random.Shared.NextDouble() * 2d) - 1d,

            _ => 0d,
        };
    }
}
