using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests;

/// <summary>
/// Unit tests for <see cref="SampleRenderer"/> LFO modulation paths:
/// Amplitude, FilterCutoff, Pitch (silent bypass), and the EnvelopeAmplitude
/// release-samples-zero branch.
/// </summary>
public sealed class SampleRendererLfoTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds a mono sine-wave <see cref="Sample"/> at 440 Hz for the given duration.
    /// </summary>
    private static Sample MakeSineSample(double durationSeconds = 0.4, int sampleRate = 44100)
    {
        var frameCount = (int)(sampleRate * durationSeconds);
        var channel = new float[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            channel[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate);
        }

        var metadata = new SampleMetadata(
            Name: "sine440",
            ChannelCount: 1,
            SampleRateHz: sampleRate,
            FrameCount: frameCount,
            Duration: TimeSpan.FromSeconds(durationSeconds),
            SourceBitsPerSample: 16,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);

        return new Sample(metadata, new[] { channel });
    }

    /// <summary>Renders and returns the raw WAV bytes from <see cref="SampleRenderer"/>.</summary>
    private static byte[] RenderToBytes(
        Sample sample,
        Envelope? envelope = null,
        FilterSettings? filter = null,
        LfoSettings? lfo = null)
    {
        var env = envelope ?? new Envelope(0, 0, 1, 0);
        using var stream = SampleRenderer.Render(
            sample,
            gain: 1.0,
            envelope: env,
            exporter: new WavSampleExporter(),
            filter: filter,
            lfo: lfo);
        return stream.ToArray();
    }

    /// <summary>
    /// Computes RMS energy of a window of PCM16 samples within a WAV byte array.
    /// </summary>
    private static double ComputeRms(byte[] wavBytes, int startSample, int endSample)
    {
        double sumSq = 0;
        var count = 0;
        for (var i = startSample; i < endSample; i++)
        {
            var pcm = BitConverter.ToInt16(wavBytes, 44 + i * 2);
            var s = pcm / 32768.0;
            sumSq += s * s;
            count++;
        }

        return count == 0 ? 0d : Math.Sqrt(sumSq / count);
    }

    // ---------------------------------------------------------------------------
    // LfoTarget.Amplitude (tremolo) on SampleRenderer
    // ---------------------------------------------------------------------------

    [Fact]
    public void SampleRenderer_LfoAmplitude_ModulatesRmsAcrossDuration()
    {
        var sample = MakeSineSample(durationSeconds: 0.4);
        var lfo = new LfoSettings(LfoTarget.Amplitude, RateHz: 5, Depth: 0.8);
        var bytes = RenderToBytes(sample, lfo: lfo);

        var sampleRate = 44100;
        var totalSamples = (bytes.Length - 44) / 2;
        var windowSize = sampleRate / 20; // 50ms windows
        var rmsValues = new List<double>();
        for (var start = 0; start + windowSize <= totalSamples; start += windowSize)
        {
            rmsValues.Add(ComputeRms(bytes, start, start + windowSize));
        }

        var min = rmsValues.Min();
        var max = rmsValues.Max();
        Assert.True(
            max - min > 0.1,
            $"Expected LfoTarget.Amplitude to vary RMS significantly across windows; min={min:F4}, max={max:F4}");
    }

    // ---------------------------------------------------------------------------
    // LfoTarget.FilterCutoff on SampleRenderer (with an active filter)
    // ---------------------------------------------------------------------------

    [Fact]
    public void SampleRenderer_LfoFilterCutoff_WithFilter_ProducesDifferentOutputThanStaticFilter()
    {
        var sample = MakeSineSample(durationSeconds: 0.2);
        var filter = new FilterSettings(FilterType.LowPass, CutoffHz: 800, Resonance: 1.0);
        var lfo = new LfoSettings(LfoTarget.FilterCutoff, RateHz: 5, Depth: 0.8);

        var withLfo = RenderToBytes(sample, filter: filter, lfo: lfo);
        var withoutLfo = RenderToBytes(sample, filter: filter);

        // Both must have the same length and valid header.
        Assert.Equal(withoutLfo.Length, withLfo.Length);

        // The PCM payload must differ — the LFO sweeps the cutoff frequency.
        var differs = false;
        for (var i = 44; i < withLfo.Length; i++)
        {
            if (withLfo[i] != withoutLfo[i])
            {
                differs = true;
                break;
            }
        }

        Assert.True(differs, "Expected LfoTarget.FilterCutoff to produce different PCM than a static filter.");
    }

    // ---------------------------------------------------------------------------
    // LfoTarget.Pitch silent bypass on SampleRenderer
    // ---------------------------------------------------------------------------

    [Fact]
    public void SampleRenderer_LfoPitch_IsSilentlyBypassed_OutputBitIdenticalToNoLfo()
    {
        // Pitch modulation is not supported for sample pads; the implementation
        // maps Pitch → None internally. Output must be bit-identical to no-LFO.
        var sample = MakeSineSample(durationSeconds: 0.2);
        var lfo = new LfoSettings(LfoTarget.Pitch, RateHz: 5, Depth: 0.8);

        var withPitchLfo = RenderToBytes(sample, lfo: lfo);
        var without = RenderToBytes(sample);

        Assert.Equal(without, withPitchLfo);
    }

    // ---------------------------------------------------------------------------
    // EnvelopeAmplitude releaseSamples <= 0 branch in SampleRenderer
    // ---------------------------------------------------------------------------

    [Fact]
    public void SampleRenderer_EnvelopeReleaseZero_RendersNonSilentOutput()
    {
        // ReleaseSeconds = 0 exercises `if (releaseSamples <= 0) return sustainLevel`
        // in SampleRenderer.EnvelopeAmplitude. The render must succeed and produce
        // non-silent audio in the sustain region.
        var sample = MakeSineSample(durationSeconds: 0.1);
        var env = new Envelope(AttackSeconds: 0.005, DecaySeconds: 0, SustainLevel: 0.8, ReleaseSeconds: 0);

        var bytes = RenderToBytes(sample, envelope: env);

        Assert.True(bytes.Length > 44, "Expected non-empty WAV output.");

        var hasNonZero = false;
        for (var i = 44; i < bytes.Length; i += 2)
        {
            if (BitConverter.ToInt16(bytes, i) != 0)
            {
                hasNonZero = true;
                break;
            }
        }

        Assert.True(hasNonZero, "Expected at least one non-zero PCM sample in the sustain phase.");
    }
}
