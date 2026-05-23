using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests;

public class WaveformSampleGeneratorTests
{
    [Fact]
    public void GeneratedSamples_AreBoundedToAudioRange()
    {
        var phase = Math.PI / 3d;
        foreach (var waveform in Enum.GetValues<WaveformType>())
        {
            var sample = WaveformSampleGenerator.NextSample(waveform, phase);
            Assert.InRange(sample, -1d, 1d);
        }
    }

    [Fact]
    public void Noise_ProducesValuesAcrossTheFullRange()
    {
        // Drawing 1000 noise samples should yield values both below and above zero,
        // confirming we map the uniform [0, 1) random source into [-1, 1).
        double min = 0d;
        double max = 0d;
        for (var i = 0; i < 1000; i++)
        {
            var sample = WaveformSampleGenerator.NextSample(WaveformType.Noise, phaseRadians: 0d);
            Assert.InRange(sample, -1d, 1d);
            if (sample < min) min = sample;
            if (sample > max) max = sample;
        }

        Assert.True(min < -0.1d, $"Expected at least one negative noise sample below -0.1; saw min={min}.");
        Assert.True(max > 0.1d, $"Expected at least one positive noise sample above 0.1; saw max={max}.");
    }

    [Fact]
    public void Noise_IgnoresPhase()
    {
        // Noise should produce different values for the same phase across calls
        // (i.e. it's truly random, not derived from the phase argument).
        var values = new HashSet<double>();
        for (var i = 0; i < 100; i++)
        {
            values.Add(WaveformSampleGenerator.NextSample(WaveformType.Noise, phaseRadians: 1.0d));
        }

        // With 100 random draws on a continuous distribution, collisions should be vanishingly rare.
        Assert.True(values.Count > 90, $"Expected near-unique noise samples; saw {values.Count} distinct of 100.");
    }
}
