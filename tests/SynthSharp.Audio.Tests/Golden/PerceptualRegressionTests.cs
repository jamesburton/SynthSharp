using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests.Golden;

/// <summary>
/// Renders known synth/sample configurations and compares the output bytes against
/// committed golden WAVs. Catches silent DSP drift across all rendering paths.
/// </summary>
public sealed class PerceptualRegressionTests
{
    private static readonly Envelope FlatEnvelope = new(0, 0, 1, 0);
    private static readonly Envelope DefaultAdsr = Envelope.Default;

    /// <summary>Pins the sine waveform at 440 Hz with the default ADSR envelope.</summary>
    [Fact]
    public void Sine440Hz_200ms_DefaultEnvelope()
    {
        using var stream = WavToneRenderer.RenderMonoPcm16(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(200), DefaultAdsr);
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "sine_440_200ms_default.wav");
    }

    /// <summary>Pins the square waveform at 440 Hz with the default ADSR envelope.</summary>
    [Fact]
    public void Square440Hz_200ms_DefaultEnvelope()
    {
        using var stream = WavToneRenderer.RenderMonoPcm16(WaveformType.Square, 440, TimeSpan.FromMilliseconds(200), DefaultAdsr);
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "square_440_200ms_default.wav");
    }

    /// <summary>Pins the sawtooth waveform at 220 Hz with the default ADSR envelope.</summary>
    [Fact]
    public void Sawtooth220Hz_200ms_DefaultEnvelope()
    {
        using var stream = WavToneRenderer.RenderMonoPcm16(WaveformType.Sawtooth, 220, TimeSpan.FromMilliseconds(200), DefaultAdsr);
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "sawtooth_220_200ms_default.wav");
    }

    /// <summary>Pins the triangle waveform at 880 Hz with the default ADSR envelope.</summary>
    [Fact]
    public void Triangle880Hz_200ms_DefaultEnvelope()
    {
        using var stream = WavToneRenderer.RenderMonoPcm16(WaveformType.Triangle, 880, TimeSpan.FromMilliseconds(200), DefaultAdsr);
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "triangle_880_200ms_default.wav");
    }

    /// <summary>Pins the BiQuad low-pass filter at 1000 Hz cutoff applied to a flat-envelope sine.</summary>
    [Fact]
    public void Sine440Hz_FlatEnvelope_LowPass1000Hz()
    {
        var filter = new FilterSettings(FilterType.LowPass, 1000, 0.707);
        using var stream = WavToneRenderer.RenderMonoPcm16(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(200), FlatEnvelope, filter);
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "sine_440_lowpass_1000.wav");
    }

    /// <summary>Pins amplitude LFO at 5 Hz depth 0.5 applied to a flat-envelope 440 Hz sine.</summary>
    [Fact]
    public void Sine440Hz_FlatEnvelope_AmplitudeLfo5Hz_Depth0_5()
    {
        var lfo = new LfoSettings(LfoTarget.Amplitude, 5, 0.5);
        using var stream = WavToneRenderer.RenderMonoPcm16(WaveformType.Sine, 440, TimeSpan.FromMilliseconds(200), FlatEnvelope, filter: null, lfo: lfo);
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "sine_440_amp_lfo_5hz.wav");
    }

    /// <summary>Pins SampleRenderer output with a ramp source, gain 0.5, and a flat envelope.</summary>
    [Fact]
    public void SampleRender_RampSample_Gain0_5_FlatEnvelope()
    {
        var sample = MakeRampSample(100);
        using var stream = SampleRenderer.Render(sample, gain: 0.5, FlatEnvelope, new WavSampleExporter());
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "sample_ramp_gain_0_5.wav");
    }

    /// <summary>Pins SampleRenderer with looping enabled over frames 25–75, extended to 500 frames.</summary>
    [Fact]
    public void SampleRender_RampSample_LoopRegion()
    {
        var sample = MakeRampSample(100);
        using var stream = SampleRenderer.Render(
            sample, gain: 1.0, FlatEnvelope, new WavSampleExporter(),
            loopEnabled: true, loopStartFrame: 25, loopEndFrame: 75, maxOutputFrames: 500);
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "sample_ramp_loop_25_75_500.wav");
    }

    /// <summary>Builds a mono ramp sample with values rising linearly from 0 to frameCount/10000.</summary>
    /// <param name="frameCount">Number of frames to generate.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    private static Sample MakeRampSample(int frameCount, int sampleRate = 44100)
    {
        var channel = new float[frameCount];
        for (var i = 0; i < frameCount; i++) channel[i] = i / 10000.0f;
        var metadata = new SampleMetadata(
            Name: "ramp",
            ChannelCount: 1,
            SampleRateHz: sampleRate,
            FrameCount: frameCount,
            Duration: TimeSpan.FromSeconds((double)frameCount / sampleRate),
            SourceBitsPerSample: 16,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);
        return new Sample(metadata, new[] { channel });
    }
}
