using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests;

/// <summary>
/// Investigates whether <see cref="NWavesPitchShifter"/> produces bit-identical output across
/// repeated calls. The original Phase 4 comment claimed phase-vocoder output varies across runs;
/// these tests are the empirical check that lets us either trust the algorithm enough to add it
/// to the perceptual harness, or document the actual non-determinism if it really is there.
/// </summary>
public sealed class PitchShiftDeterminismTests
{
    private static Sample MakeSineSample(double freqHz, double durationSeconds, int sampleRate = 44100)
    {
        var frameCount = (int)(sampleRate * durationSeconds);
        var channel = new float[frameCount];
        for (var f = 0; f < frameCount; f++)
        {
            channel[f] = (float)Math.Sin(2 * Math.PI * freqHz * (f / (double)sampleRate));
        }

        var metadata = new SampleMetadata(
            Name: "sine",
            ChannelCount: 1,
            SampleRateHz: sampleRate,
            FrameCount: frameCount,
            Duration: TimeSpan.FromSeconds(durationSeconds),
            SourceBitsPerSample: 32,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);

        return new Sample(metadata, new[] { channel });
    }

    [Fact]
    public void Shift_SameInputAndShifter_ProducesBitIdenticalOutput()
    {
        var shifter = new NWavesPitchShifter();
        var source = MakeSineSample(440, 0.25);

        var first = shifter.Shift(source, semitones: 7);
        var second = shifter.Shift(source, semitones: 7);

        Assert.Equal(first.Channels[0].Length, second.Channels[0].Length);
        for (var i = 0; i < first.Channels[0].Length; i++)
        {
            Assert.True(
                first.Channels[0][i] == second.Channels[0][i],
                $"Sample {i} differs: first={first.Channels[0][i]} second={second.Channels[0][i]}");
        }
    }

    [Fact]
    public void Shift_FreshShifterInstances_ProducesBitIdenticalOutput()
    {
        var source = MakeSineSample(440, 0.25);

        var first = new NWavesPitchShifter().Shift(source, semitones: 7);
        var second = new NWavesPitchShifter().Shift(source, semitones: 7);

        Assert.Equal(first.Channels[0].Length, second.Channels[0].Length);
        for (var i = 0; i < first.Channels[0].Length; i++)
        {
            Assert.True(
                first.Channels[0][i] == second.Channels[0][i],
                $"Sample {i} differs across fresh shifter instances: first={first.Channels[0][i]} second={second.Channels[0][i]}");
        }
    }

    [Fact]
    public void Shift_DifferentSemitones_ProducesDifferentOutput()
    {
        // Sanity check: if shifting by different semitone counts produces the same output,
        // determinism is meaningless because the shift isn't actually doing anything.
        var shifter = new NWavesPitchShifter();
        var source = MakeSineSample(440, 0.25);

        var up = shifter.Shift(source, semitones: 7);
        var down = shifter.Shift(source, semitones: -7);

        var anyDiff = false;
        for (var i = 0; i < up.Channels[0].Length; i++)
        {
            if (up.Channels[0][i] != down.Channels[0][i])
            {
                anyDiff = true;
                break;
            }
        }
        Assert.True(anyDiff, "Up-shift and down-shift produced identical output; the shifter is a no-op.");
    }
}
