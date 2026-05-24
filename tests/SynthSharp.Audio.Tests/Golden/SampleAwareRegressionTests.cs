using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests.Golden;

/// <summary>
/// Perceptual goldens for SampleRenderer against an instrument-like fixture (synthetic plucked
/// sawtooth at 220 Hz, 200 ms, with envelope decay and low-pass filter). Catches regressions
/// where rendering changes behaviour on real-world sample shapes — transients, envelope decays,
/// near-clipping peaks — that the existing ramp-array goldens don't exercise.
/// </summary>
public sealed class SampleAwareRegressionTests
{
    private static readonly Envelope FlatEnvelope = new(0, 0, 1, 0);

    private static Sample LoadFixture()
    {
        var path = InstrumentFixtureFactory.EnsureFixturePath();
        using var stream = File.OpenRead(path);
        return new WavSampleImporter().Import(stream, sourcePath: path);
    }

    /// <summary>Pins SampleRenderer raw playback of the instrument fixture at unity gain.</summary>
    [Fact]
    public void InstrumentSample_RawPlayback()
    {
        var sample = LoadFixture();
        using var stream = SampleRenderer.Render(sample, gain: 1.0, FlatEnvelope, new WavSampleExporter());
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "instrument_raw.wav");
    }

    /// <summary>Pins SampleRenderer with gain reduced to 0.5 against the instrument fixture.</summary>
    [Fact]
    public void InstrumentSample_GainHalf()
    {
        var sample = LoadFixture();
        using var stream = SampleRenderer.Render(sample, gain: 0.5, FlatEnvelope, new WavSampleExporter());
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "instrument_gain_0_5.wav");
    }

    /// <summary>Pins SampleRenderer with a 500 Hz low-pass filter applied to the instrument fixture.</summary>
    [Fact]
    public void InstrumentSample_LowPass500()
    {
        var sample = LoadFixture();
        var filter = new FilterSettings(FilterType.LowPass, 500, 0.707);
        using var stream = SampleRenderer.Render(sample, gain: 1.0, FlatEnvelope, new WavSampleExporter(), filter: filter);
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "instrument_lowpass_500.wav");
    }

    /// <summary>Pins SampleRenderer trimmed to frames 2000–6000 of the instrument fixture.</summary>
    [Fact]
    public void InstrumentSample_TrimMidSection()
    {
        var sample = LoadFixture();
        using var stream = SampleRenderer.Render(
            sample, gain: 1.0, FlatEnvelope, new WavSampleExporter(),
            trimStartFrame: 2000, trimEndFrame: 6000);
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "instrument_trim_2000_6000.wav");
    }

    /// <summary>Pins SampleRenderer with looping enabled over frames 5000–8000, extended to 12000 output frames.</summary>
    [Fact]
    public void InstrumentSample_LoopInTail()
    {
        var sample = LoadFixture();
        using var stream = SampleRenderer.Render(
            sample, gain: 1.0, FlatEnvelope, new WavSampleExporter(),
            loopEnabled: true, loopStartFrame: 5000, loopEndFrame: 8000, maxOutputFrames: 12000);
        GoldenAudio.AssertMatchesGolden(stream.ToArray(), "instrument_loop_5000_8000_12000.wav");
    }
}
