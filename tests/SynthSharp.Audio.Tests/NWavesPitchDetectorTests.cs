using SynthSharp.Audio;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Music;

namespace SynthSharp.Audio.Tests;

/// <summary>Unit tests for <see cref="NWavesPitchDetector"/> covering sine, sawtooth, silence, and edge cases.</summary>
public sealed class NWavesPitchDetectorTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>Builds a mono or multi-channel <see cref="Sample"/> containing a pure sine wave.</summary>
    private static Sample MakeSineSample(
        double freqHz,
        double durationSeconds,
        int sampleRate = 44100,
        int channels = 1)
    {
        var frameCount = (int)(sampleRate * durationSeconds);
        var channelData = new float[channels][];

        for (var c = 0; c < channels; c++)
        {
            channelData[c] = new float[frameCount];
            for (var f = 0; f < frameCount; f++)
            {
                var t = (double)f / sampleRate;
                channelData[c][f] = (float)Math.Sin(2 * Math.PI * freqHz * t);
            }
        }

        var metadata = new SampleMetadata(
            Name: $"sine_{freqHz}Hz",
            ChannelCount: channels,
            SampleRateHz: sampleRate,
            FrameCount: frameCount,
            Duration: TimeSpan.FromSeconds(durationSeconds),
            SourceBitsPerSample: 32,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);

        return new Sample(metadata, channelData);
    }

    /// <summary>Builds a mono sawtooth wave sample.</summary>
    private static Sample MakeSawtoothSample(
        double freqHz,
        double durationSeconds,
        int sampleRate = 44100)
    {
        var frameCount = (int)(sampleRate * durationSeconds);
        var channel = new float[frameCount];

        for (var f = 0; f < frameCount; f++)
        {
            var t = (double)f / sampleRate;

            // Bandlimited sawtooth via the standard formula: value in [-1, 1].
            channel[f] = (float)(2.0 * (t * freqHz - Math.Floor(t * freqHz + 0.5)));
        }

        var metadata = new SampleMetadata(
            Name: $"saw_{freqHz}Hz",
            ChannelCount: 1,
            SampleRateHz: sampleRate,
            FrameCount: frameCount,
            Duration: TimeSpan.FromSeconds(durationSeconds),
            SourceBitsPerSample: 32,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);

        return new Sample(metadata, new[] { channel });
    }

    /// <summary>Builds a sample with every frame set to zero.</summary>
    private static Sample MakeSilenceSample(double durationSeconds, int sampleRate = 44100)
    {
        var frameCount = (int)(sampleRate * durationSeconds);
        var metadata = new SampleMetadata(
            Name: "silence",
            ChannelCount: 1,
            SampleRateHz: sampleRate,
            FrameCount: frameCount,
            Duration: TimeSpan.FromSeconds(durationSeconds),
            SourceBitsPerSample: 32,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);

        return new Sample(metadata, new[] { new float[frameCount] });
    }

    // ---------------------------------------------------------------------------
    // Pitch detection — sine waves
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detects_440Hz_Sine_WithinTwoHz()
    {
        var detector = new NWavesPitchDetector();
        var sample = MakeSineSample(440, durationSeconds: 1.0);

        var result = detector.Estimate(sample, new PitchDetectionOptions(CmdfThreshold: 0.10f));

        Assert.InRange(result.FundamentalHz, 438f, 442f);
        Assert.True(result.ConfidenceScore >= 0.7f,
            $"Expected confidence >= 0.7 but got {result.ConfidenceScore}");
    }

    [Fact]
    public void Detects_880Hz_Sine()
    {
        var detector = new NWavesPitchDetector();
        var sample = MakeSineSample(880, durationSeconds: 1.0);

        var result = detector.Estimate(sample, new PitchDetectionOptions(CmdfThreshold: 0.10f));

        Assert.InRange(result.FundamentalHz, 875f, 885f);
    }

    [Fact]
    public void Detects_110Hz_Sine()
    {
        var detector = new NWavesPitchDetector();
        var sample = MakeSineSample(110, durationSeconds: 1.0);

        var result = detector.Estimate(sample, new PitchDetectionOptions(CmdfThreshold: 0.10f));

        Assert.InRange(result.FundamentalHz, 108f, 112f);
    }

    // ---------------------------------------------------------------------------
    // Pitch detection — sawtooth
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detects_440Hz_Sawtooth()
    {
        var detector = new NWavesPitchDetector();
        var sample = MakeSawtoothSample(440, durationSeconds: 1.0);

        var result = detector.Estimate(sample, new PitchDetectionOptions(CmdfThreshold: 0.10f));

        Assert.InRange(result.FundamentalHz, 435f, 445f);
        Assert.True(result.ConfidenceScore >= 0.5f,
            $"Expected confidence >= 0.5 but got {result.ConfidenceScore}");
    }

    // ---------------------------------------------------------------------------
    // Silence and short-signal edge cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void Silence_Returns_ZeroFundamental()
    {
        var detector = new NWavesPitchDetector();
        var sample = MakeSilenceSample(durationSeconds: 1.0);

        var result = detector.Estimate(sample, new PitchDetectionOptions(EmitPerFrameEstimates: true));

        Assert.Equal(0f, result.FundamentalHz);
        Assert.Equal(0f, result.ConfidenceScore);

        // Even with EmitPerFrameEstimates=true, silence produces no detectable pitch in any frame,
        // so the per-frame list is empty — a clean "nothing detected" signal.
        Assert.Empty(result.PerFrameEstimates);
    }

    [Fact]
    public void ShortSample_ReturnsZero_NoException()
    {
        var detector = new NWavesPitchDetector();

        // 100 frames < default FrameSizeSamples (2048) — should return gracefully with 0.
        var metadata = new SampleMetadata(
            Name: "short",
            ChannelCount: 1,
            SampleRateHz: 44100,
            FrameCount: 100,
            Duration: TimeSpan.FromSeconds(100d / 44100),
            SourceBitsPerSample: 32,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);
        var sample = new Sample(metadata, new[] { new float[100] });

        var result = detector.Estimate(sample);

        Assert.Equal(0f, result.FundamentalHz);
    }

    // ---------------------------------------------------------------------------
    // Stereo downmix
    // ---------------------------------------------------------------------------

    [Fact]
    public void Stereo_IdenticalChannels_DetectsSameFundamental_AsMono()
    {
        var detector = new NWavesPitchDetector();
        var monoSample = MakeSineSample(440, durationSeconds: 1.0, channels: 1);
        var stereoSample = MakeSineSample(440, durationSeconds: 1.0, channels: 2);

        var opts = new PitchDetectionOptions(CmdfThreshold: 0.10f);
        var monoResult = detector.Estimate(monoSample, opts);
        var stereoResult = detector.Estimate(stereoSample, opts);

        Assert.InRange(Math.Abs(stereoResult.FundamentalHz - monoResult.FundamentalHz), 0f, 1f);
    }

    [Fact]
    public void Stereo_OppositePolarity_Downmix_Returns_Zero()
    {
        var detector = new NWavesPitchDetector();
        var frameCount = 44100;
        var sineData = new float[frameCount];

        for (var f = 0; f < frameCount; f++)
        {
            var t = (double)f / 44100;
            sineData[f] = (float)Math.Sin(2 * Math.PI * 440 * t);
        }

        // L = sin, R = -sin; downmix = (sin + -sin) / 2 = 0
        var negated = sineData.Select(x => -x).ToArray();

        var metadata = new SampleMetadata(
            Name: "opposite_polarity",
            ChannelCount: 2,
            SampleRateHz: 44100,
            FrameCount: frameCount,
            Duration: TimeSpan.FromSeconds(1.0),
            SourceBitsPerSample: 32,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);
        var sample = new Sample(metadata, new[] { sineData, negated });

        var result = detector.Estimate(sample);

        Assert.Equal(0f, result.FundamentalHz);
    }

    // ---------------------------------------------------------------------------
    // PerFrameEstimates flag
    // ---------------------------------------------------------------------------

    [Fact]
    public void EmitPerFrameEstimates_True_ReturnsPopulatedList()
    {
        var detector = new NWavesPitchDetector();
        var sample = MakeSineSample(440, durationSeconds: 1.0);

        var result = detector.Estimate(sample, new PitchDetectionOptions(
            EmitPerFrameEstimates: true,
            CmdfThreshold: 0.10f));

        Assert.True(result.PerFrameEstimates.Count > 0,
            "Expected per-frame list to be populated when EmitPerFrameEstimates=true.");
    }

    [Fact]
    public void EmitPerFrameEstimates_False_ReturnsEmptyList()
    {
        var detector = new NWavesPitchDetector();
        var sample = MakeSineSample(440, durationSeconds: 1.0);

        var result = detector.Estimate(sample, new PitchDetectionOptions(
            EmitPerFrameEstimates: false,
            CmdfThreshold: 0.10f));

        Assert.Empty(result.PerFrameEstimates);
    }

    // ---------------------------------------------------------------------------
    // Guard clauses
    // ---------------------------------------------------------------------------

    [Fact]
    public void Throws_OnNullSample()
    {
        var detector = new NWavesPitchDetector();

        Assert.Throws<ArgumentNullException>(() => detector.Estimate(null!));
    }

    [Fact]
    public void Throws_OnZeroFrameSample()
    {
        var detector = new NWavesPitchDetector();

        // FrameCount=0 with matching empty channel is valid construction per Sample's contract.
        var metadata = new SampleMetadata(
            Name: "empty",
            ChannelCount: 1,
            SampleRateHz: 44100,
            FrameCount: 0,
            Duration: TimeSpan.Zero,
            SourceBitsPerSample: 32,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);
        var sample = new Sample(metadata, new[] { Array.Empty<float>() });

        Assert.Throws<ArgumentException>(() => detector.Estimate(sample));
    }

    [Fact]
    public void Throws_WhenFrameSizeTooSmallForMinHz_GuardsAgainstNwavesIssue88()
    {
        // Repro of NWaves issue #88: 256-sample window at 22050 Hz with MinHz=40 Hz.
        // Our guard must surface ArgumentException before NWaves itself hits IndexOutOfRange.
        var detector = new NWavesPitchDetector();
        var sample = MakeSineSample(freqHz: 220, durationSeconds: 0.5, sampleRate: 22050);
        var options = new PitchDetectionOptions(
            MinHz: 40f,
            MaxHz: 700f,
            FrameSizeSamples: 256,
            HopSizeSamples: 128);

        var ex = Assert.Throws<ArgumentException>(() => detector.Estimate(sample, options));
        Assert.Contains("FrameSizeSamples", ex.Message);
        Assert.Contains("MinHz", ex.Message);
    }
}
