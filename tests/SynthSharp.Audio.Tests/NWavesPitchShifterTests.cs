using NwPitch = NWaves.Features.Pitch;
using SynthSharp.Audio;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Music;

namespace SynthSharp.Audio.Tests;

/// <summary>Unit tests for <see cref="NWavesPitchShifter"/>.</summary>
public sealed class NWavesPitchShifterTests
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

    /// <summary>
    /// Detects the fundamental pitch of channel 0 in <paramref name="sample"/> using a
    /// single-frame YIN call over the entire channel. The single-frame approach is more
    /// reliable on phase-vocoded signals than the multi-frame windowed path in
    /// <see cref="NWavesPitchDetector"/>, because windowed multi-frame detection on vocoder
    /// output can latch onto sub-harmonics introduced by phase smearing.
    /// </summary>
    /// <remarks>
    /// Uses the NWaves default cmdfThreshold of 0.2. A stricter threshold (e.g. 0.10) causes
    /// false negatives on vocoded signals whose CMDF periodicity exceeds the strict bound.
    /// </remarks>
    private static float DetectPitch(Sample sample, float minHz = 80f, float maxHz = 2000f)
    {
        // Use channel 0 directly; stereo samples still verify correctly for pitch tests.
        // cmdfThreshold left at the NWaves default (0.2) — see remarks.
        return NwPitch.FromYin(
            sample.Channels[0],
            sample.Metadata.SampleRateHz,
            low: minHz,
            high: maxHz);
    }

    // ---------------------------------------------------------------------------
    // Zero semitones — identity
    // ---------------------------------------------------------------------------

    [Fact]
    public void Shift_ZeroSemitones_ReturnsSource()
    {
        var shifter = new NWavesPitchShifter();
        var source = MakeSineSample(440, durationSeconds: 1.0);

        var result = shifter.Shift(source, 0);

        // Contract: semitones==0 returns the same instance, not a copy.
        Assert.Same(source, result);
    }

    // ---------------------------------------------------------------------------
    // Octave shifts — cardinal pitch-doubling / halving checks
    // ---------------------------------------------------------------------------

    [Fact]
    public void Shift_UpOneOctave_DoublesDetectedPitch()
    {
        var shifter = new NWavesPitchShifter();
        var source = MakeSineSample(440, durationSeconds: 1.0);

        var shifted = shifter.Shift(source, semitones: 12);

        // Target: 880 Hz; allow ±5% tolerance (~836–924 Hz).
        var detected = DetectPitch(shifted);
        Assert.True(detected >= 860f && detected <= 920f, $"Expected detected pitch in [860, 920] Hz but got {detected} Hz");
    }

    [Fact]
    public void Shift_DownOneOctave_HalvesDetectedPitch()
    {
        var shifter = new NWavesPitchShifter();
        var source = MakeSineSample(440, durationSeconds: 1.0);

        var shifted = shifter.Shift(source, semitones: -12);

        // Target: 220 Hz; allow ±5% tolerance (~209–231 Hz).
        var detected = DetectPitch(shifted);
        Assert.InRange(detected, 210f, 230f);
    }

    // ---------------------------------------------------------------------------
    // Single-semitone equal-temperament ratio checks
    // ---------------------------------------------------------------------------

    [Fact]
    public void Shift_PlusOneSemitone_RatioMatchesEqualTemperament()
    {
        var shifter = new NWavesPitchShifter();
        var source = MakeSineSample(440, durationSeconds: 1.0);

        var shifted = shifter.Shift(source, semitones: 1);

        // 440 × 2^(1/12) ≈ 466.16 Hz; allow ±5 Hz.
        var detected = DetectPitch(shifted);
        Assert.InRange(detected, 461f, 471f);
    }

    [Fact]
    public void Shift_MinusOneSemitone_RatioMatchesEqualTemperament()
    {
        var shifter = new NWavesPitchShifter();
        var source = MakeSineSample(440, durationSeconds: 1.0);

        var shifted = shifter.Shift(source, semitones: -1);

        // 440 / 2^(1/12) ≈ 415.30 Hz; allow ±5 Hz.
        var detected = DetectPitch(shifted);
        Assert.InRange(detected, 410f, 420f);
    }

    // ---------------------------------------------------------------------------
    // Channel count and metadata consistency
    // ---------------------------------------------------------------------------

    [Fact]
    public void Shift_StereoSample_PreservesChannelCount()
    {
        var shifter = new NWavesPitchShifter();
        var source = MakeSineSample(440, durationSeconds: 1.0, channels: 2);

        var result = shifter.Shift(source, semitones: 7);

        Assert.Equal(2, result.Metadata.ChannelCount);
        Assert.Equal(2, result.Channels.Count);
    }

    [Fact]
    public void Shift_OutputMetadataIsConsistent()
    {
        var shifter = new NWavesPitchShifter();
        var source = MakeSineSample(440, durationSeconds: 1.0, channels: 2);

        var result = shifter.Shift(source, semitones: 5);

        Assert.Equal(result.Metadata.ChannelCount, result.Channels.Count);
        for (var i = 0; i < result.Channels.Count; i++)
        {
            Assert.Equal(result.Metadata.FrameCount, result.Channels[i].Length);
        }
    }

    // ---------------------------------------------------------------------------
    // Guard clauses
    // ---------------------------------------------------------------------------

    [Fact]
    public void Shift_ThrowsOnNullSource()
    {
        var shifter = new NWavesPitchShifter();

        Assert.Throws<ArgumentNullException>(() => shifter.Shift(null!, semitones: 1));
    }

    [Fact]
    public void Shift_OutputFrameCount_AlwaysMatchesSourceFrameCount()
    {
        // NormaliseLength must truncate or zero-pad the phase vocoder output so that the
        // returned sample's FrameCount always equals the source's FrameCount.
        // This exercises the length-normalisation paths in NWavesPitchShifter (including
        // the truncation branch when the vocoder output is longer than the input).
        var shifter = new NWavesPitchShifter();

        // Use multiple durations and shift amounts; shorter signals tend to produce
        // relative over-run from hop-padding in the phase vocoder.
        int[] semitoneValues = [1, 5, 12, -7];
        double[] durationValues = [0.05, 0.1, 0.5];

        foreach (var dur in durationValues)
        {
            var source = MakeSineSample(440, durationSeconds: dur);
            foreach (var semitones in semitoneValues)
            {
                var result = shifter.Shift(source, semitones);
                Assert.Equal(source.Metadata.FrameCount, result.Metadata.FrameCount);
                for (var c = 0; c < result.Channels.Count; c++)
                {
                    Assert.Equal(result.Metadata.FrameCount, result.Channels[c].Length);
                }
            }
        }
    }
}
