using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests;

public sealed class LfoRenderingTests
{
    private static byte[] RenderToBytes(WaveformType wave, double freq, TimeSpan duration, Envelope env, FilterSettings? filter = null, LfoSettings? lfo = null)
    {
        using var stream = WavToneRenderer.RenderMonoPcm16(wave, freq, duration, env, filter, lfo);
        return stream.ToArray();
    }

    private static double ComputeRms(byte[] wavBytes, int startSample, int endSample)
    {
        double sumSquares = 0;
        var count = 0;
        for (var i = startSample; i < endSample; i++)
        {
            var pcm = BitConverter.ToInt16(wavBytes, 44 + i * 2);
            var s = pcm / 32768.0;
            sumSquares += s * s;
            count++;
        }

        return count == 0 ? 0 : Math.Sqrt(sumSquares / count);
    }

    [Fact]
    public void Lfo_Off_MatchesNoLfoOverload()
    {
        var env = new Envelope(0, 0, 1, 0);
        var withOff = RenderToBytes(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(200), env, lfo: LfoSettings.Off);
        var without = RenderToBytes(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(200), env);
        Assert.Equal(without, withOff);
    }

    [Fact]
    public void Lfo_Amplitude_ModulatesRmsAcrossDuration()
    {
        var env = new Envelope(0, 0, 1, 0);
        var lfo = new LfoSettings(LfoTarget.Amplitude, RateHz: 5, Depth: 0.8);
        var bytes = RenderToBytes(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(400), env, lfo: lfo);

        var sampleRate = 44100;
        var totalSamples = (bytes.Length - 44) / 2;
        var windowSize = sampleRate / 20; // 50ms windows
        var rmsValues = new List<double>();
        for (var start = 0; start + windowSize < totalSamples; start += windowSize)
        {
            rmsValues.Add(ComputeRms(bytes, start, start + windowSize));
        }

        var min = rmsValues.Min();
        var max = rmsValues.Max();
        Assert.True(max - min > 0.1, $"Expected modulated RMS to vary significantly; got min={min}, max={max}.");
    }

    [Fact]
    public void Lfo_TargetNone_BypassesModulation()
    {
        var env = new Envelope(0, 0, 1, 0);
        var lfo = new LfoSettings(LfoTarget.None, RateHz: 5, Depth: 0.8);
        var withNone = RenderToBytes(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(200), env, lfo: lfo);
        var without = RenderToBytes(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(200), env);
        Assert.Equal(without, withNone);
    }

    [Fact]
    public void Lfo_FilterCutoff_RequiresFilterToHaveEffect()
    {
        var env = new Envelope(0, 0, 1, 0);
        var lfo = new LfoSettings(LfoTarget.FilterCutoff, RateHz: 5, Depth: 0.8);

        // Without a filter, FilterCutoff LFO is a no-op.
        var withLfoNoFilter = RenderToBytes(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(200), env, lfo: lfo);
        var without = RenderToBytes(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(200), env);
        Assert.Equal(without, withLfoNoFilter);
    }
}
