using NWaves.Filters.BiQuad;
using SynthSharp.Audio;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Persistence;

namespace SynthSharp.Audio.Tests;

/// <summary>Unit tests for <see cref="AudioFilters"/>, filter integration in <see cref="WavToneRenderer"/>, and filter integration in <see cref="SampleRenderer"/>.</summary>
public sealed class AudioFiltersTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>Computes the RMS energy of the PCM16 samples in a WAV byte array (skips the 44-byte header).</summary>
    private static double ComputeRms(byte[] wavBytes)
    {
        var sampleCount = (wavBytes.Length - 44) / 2;
        if (sampleCount <= 0)
        {
            return 0;
        }

        double sumSquares = 0;
        for (var i = 0; i < sampleCount; i++)
        {
            var pcm = BitConverter.ToInt16(wavBytes, 44 + (i * 2));
            var s = pcm / 32768.0;
            sumSquares += s * s;
        }

        return Math.Sqrt(sumSquares / sampleCount);
    }

    /// <summary>Renders a sine wave via <see cref="WavToneRenderer"/> and returns the bytes.</summary>
    private static byte[] RenderSineWav(double freqHz, FilterSettings? filter = null)
    {
        var envelope = new Envelope(
            AttackSeconds: 0d,
            DecaySeconds: 0d,
            SustainLevel: 1d,
            ReleaseSeconds: 0d);

        using var stream = WavToneRenderer.RenderMonoPcm16(
            WaveformType.Sine,
            freqHz,
            TimeSpan.FromSeconds(0.5),
            envelope,
            filter);

        return stream.ToArray();
    }

    /// <summary>Builds an in-memory mono <see cref="Sample"/> containing a pure sine wave.</summary>
    private static Sample MakeSineSample(double freqHz, int sampleRate = 44100, double durationSeconds = 0.5)
    {
        var frameCount = (int)(sampleRate * durationSeconds);
        var channel = new float[frameCount];
        for (var f = 0; f < frameCount; f++)
        {
            channel[f] = (float)Math.Sin(2 * Math.PI * freqHz * f / sampleRate);
        }

        var metadata = new SampleMetadata(
            Name: $"sine_{freqHz}Hz",
            ChannelCount: 1,
            SampleRateHz: sampleRate,
            FrameCount: frameCount,
            Duration: TimeSpan.FromSeconds(durationSeconds),
            SourceBitsPerSample: 32,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);

        return new Sample(metadata, new[] { channel });
    }

    // ---------------------------------------------------------------------------
    // FilterSettings and FilterType basics
    // ---------------------------------------------------------------------------

    [Fact]
    public void FilterSettings_Off_HasTypeNone()
    {
        Assert.Equal(FilterType.None, FilterSettings.Off.Type);
    }

    // ---------------------------------------------------------------------------
    // AudioFilters.Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void AudioFilters_Create_None_ReturnsNull()
    {
        var result = AudioFilters.Create(FilterSettings.Off, 44100);
        Assert.Null(result);
    }

    [Fact]
    public void AudioFilters_Create_LowPass_ReturnsFilter()
    {
        var settings = new FilterSettings(FilterType.LowPass, 1000d, 0.707d);
        var result = AudioFilters.Create(settings, 44100);
        Assert.NotNull(result);
        Assert.IsAssignableFrom<BiQuadFilter>(result);
    }

    [Fact]
    public void AudioFilters_Create_HighPass_ReturnsFilter()
    {
        var settings = new FilterSettings(FilterType.HighPass, 1000d, 0.707d);
        var result = AudioFilters.Create(settings, 44100);
        Assert.NotNull(result);
    }

    [Fact]
    public void AudioFilters_Create_BandPass_ReturnsFilter()
    {
        var settings = new FilterSettings(FilterType.BandPass, 1000d, 0.707d);
        var result = AudioFilters.Create(settings, 44100);
        Assert.NotNull(result);
    }

    [Fact]
    public void AudioFilters_Create_ZeroSampleRate_ReturnsNull()
    {
        var settings = new FilterSettings(FilterType.LowPass, 1000d, 0.707d);
        var result = AudioFilters.Create(settings, 0);
        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // WavToneRenderer filter integration
    // ---------------------------------------------------------------------------

    [Fact]
    public void Render_NoFilter_HighRmsBaseline()
    {
        // A full-amplitude sustain sine should have RMS near 1/sqrt(2) ≈ 0.707.
        var bytes = RenderSineWav(440d);
        var rms = ComputeRms(bytes);
        Assert.True(rms > 0.5, $"Expected RMS > 0.5 for unfiltered 440 Hz sine, got {rms:F4}");
    }

    [Fact]
    public void Render_LowPassAt200_AttenuatesHighFrequencies()
    {
        // 440 Hz is above the 200 Hz cutoff — significant attenuation expected.
        var baseline = ComputeRms(RenderSineWav(440d));
        var filtered = ComputeRms(RenderSineWav(440d, new FilterSettings(FilterType.LowPass, 200d, 0.707d)));
        Assert.True(filtered < baseline * 0.5,
            $"LowPass at 200 Hz should attenuate 440 Hz by >50 %. Baseline={baseline:F4}, Filtered={filtered:F4}");
    }

    [Fact]
    public void Render_LowPassAt4000_PreservesPassband()
    {
        // 440 Hz is well within the 4000 Hz passband — less than 30 % attenuation.
        var baseline = ComputeRms(RenderSineWav(440d));
        var filtered = ComputeRms(RenderSineWav(440d, new FilterSettings(FilterType.LowPass, 4000d, 0.707d)));
        Assert.True(filtered > baseline * 0.70,
            $"LowPass at 4000 Hz should pass 440 Hz within 30 %. Baseline={baseline:F4}, Filtered={filtered:F4}");
    }

    [Fact]
    public void Render_HighPassAt1000_AttenuatesLowFrequencies()
    {
        // 440 Hz is below the 1000 Hz cutoff — significant attenuation expected.
        var baseline = ComputeRms(RenderSineWav(440d));
        var filtered = ComputeRms(RenderSineWav(440d, new FilterSettings(FilterType.HighPass, 1000d, 0.707d)));
        Assert.True(filtered < baseline * 0.5,
            $"HighPass at 1000 Hz should attenuate 440 Hz by >50 %. Baseline={baseline:F4}, Filtered={filtered:F4}");
    }

    [Fact]
    public void Render_FilterOff_MatchesNoFilterOverload()
    {
        // Passing FilterSettings.Off explicitly must produce byte-identical output to the null path.
        var withNull = RenderSineWav(440d, filter: null);
        var withOff = RenderSineWav(440d, filter: FilterSettings.Off);
        Assert.Equal(withNull, withOff);
    }

    // ---------------------------------------------------------------------------
    // SampleRenderer filter integration
    // ---------------------------------------------------------------------------

    [Fact]
    public void SampleRender_LowPass_AttenuatesHighFrequencies()
    {
        var exporter = new WavSampleExporter();
        var envelope = new Envelope(AttackSeconds: 0d, DecaySeconds: 0d, SustainLevel: 1d, ReleaseSeconds: 0d);
        var sine440 = MakeSineSample(440d);

        using var baselineStream = SampleRenderer.Render(sine440, gain: 1d, envelope, exporter);
        var baseline = ComputeRms(baselineStream.ToArray());

        using var filteredStream = SampleRenderer.Render(
            sine440, gain: 1d, envelope, exporter,
            filter: new FilterSettings(FilterType.LowPass, 200d, 0.707d));
        var filtered = ComputeRms(filteredStream.ToArray());

        Assert.True(filtered < baseline * 0.5,
            $"SampleRenderer LowPass at 200 Hz should attenuate 440 Hz by >50 %. Baseline={baseline:F4}, Filtered={filtered:F4}");
    }

    [Fact]
    public void SampleRender_FilterOff_MatchesNoFilterOverload()
    {
        var exporter = new WavSampleExporter();
        var envelope = new Envelope(AttackSeconds: 0d, DecaySeconds: 0d, SustainLevel: 1d, ReleaseSeconds: 0d);
        var sine440 = MakeSineSample(440d);

        using var withNull = SampleRenderer.Render(sine440, gain: 1d, envelope, exporter, filter: null);
        using var withOff = SampleRenderer.Render(sine440, gain: 1d, envelope, exporter, filter: FilterSettings.Off);

        Assert.Equal(withNull.ToArray(), withOff.ToArray());
    }
}
