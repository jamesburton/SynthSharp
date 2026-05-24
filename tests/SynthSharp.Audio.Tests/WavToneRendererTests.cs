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

    [Fact]
    public void RenderMonoPcm16_LfoPitch_ProducesDifferentOutputThanNoLfo()
    {
        // LfoTarget.Pitch uses an incremental phase accumulator path in WavToneRenderer.
        // The output must differ from the no-LFO render (frequency modulation changes phase
        // accumulation) and the WAV header must remain valid.
        var env = new Envelope(0, 0, 1, 0);
        var lfo = new LfoSettings(LfoTarget.Pitch, RateHz: 5, Depth: 1.0);

        using var withLfo = WavToneRenderer.RenderMonoPcm16(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(200), env, lfo: lfo);
        using var without = WavToneRenderer.RenderMonoPcm16(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(200), env);

        var withLfoBytes = withLfo.ToArray();
        var withoutBytes = without.ToArray();

        // Both streams must have the same length and valid RIFF header.
        Assert.Equal(withoutBytes.Length, withLfoBytes.Length);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(withLfoBytes, 0, 4));

        // The PCM payload must differ — the pitch LFO accumulator changes sample values.
        var payloadDiffers = false;
        for (var i = 44; i < withLfoBytes.Length; i++)
        {
            if (withLfoBytes[i] != withoutBytes[i])
            {
                payloadDiffers = true;
                break;
            }
        }

        Assert.True(payloadDiffers, "Expected LfoTarget.Pitch to produce different PCM samples from the no-LFO render.");
    }

    [Fact]
    public void RenderMonoPcm16_EnvelopeReleaseZero_RendersNonSilentOutput()
    {
        // ReleaseSeconds = 0 triggers the `if (releaseSamples <= 0) return sustainLevel`
        // branch in EnvelopeAmplitude. The render must succeed and produce non-silent audio.
        var env = new Envelope(AttackSeconds: 0.01, DecaySeconds: 0, SustainLevel: 0.8, ReleaseSeconds: 0);

        using var stream = WavToneRenderer.RenderMonoPcm16(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(100), env);
        var bytes = stream.ToArray();

        Assert.True(bytes.Length > 44, "Expected non-empty WAV output.");

        // At least one non-zero PCM sample expected in the sustain region.
        var hasNonZero = false;
        for (var i = 44; i < bytes.Length; i += 2)
        {
            var pcm = BitConverter.ToInt16(bytes, i);
            if (pcm != 0)
            {
                hasNonZero = true;
                break;
            }
        }

        Assert.True(hasNonZero, "Expected at least one non-zero PCM sample in the sustain phase.");
    }
}
