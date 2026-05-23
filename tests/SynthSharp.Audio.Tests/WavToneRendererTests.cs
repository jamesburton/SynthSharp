using System.Text;
using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests;

public class WavToneRendererTests
{
    [Fact]
    public void RenderMonoPcm16_CreatesWaveHeader()
    {
        using var stream = WavToneRenderer.RenderMonoPcm16(
            WaveformType.Sine,
            440d,
            TimeSpan.FromMilliseconds(100),
            Envelope.Default);

        var buffer = stream.ToArray();
        Assert.True(buffer.Length > 44);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(buffer, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(buffer, 8, 4));
        Assert.Equal("data", Encoding.ASCII.GetString(buffer, 36, 4));
    }
}
