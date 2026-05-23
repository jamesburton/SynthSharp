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
}
