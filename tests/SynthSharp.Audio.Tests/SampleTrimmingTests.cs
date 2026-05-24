using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests;

/// <summary>Unit tests for <see cref="SampleRenderer"/>'s trim-region behaviour.</summary>
public sealed class SampleTrimmingTests
{
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

    private static byte[] Render(
        Sample sample,
        int trimStart = 0,
        int trimEnd = 0,
        bool loopEnabled = false,
        int loopStart = 0,
        int loopEnd = 0,
        int maxOutputFrames = 0)
    {
        var flat = new Envelope(0, 0, 1, 0);
        using var stream = SampleRenderer.Render(
            sample, 1.0, flat, new WavSampleExporter(),
            loopEnabled: loopEnabled, loopStartFrame: loopStart, loopEndFrame: loopEnd,
            maxOutputFrames: maxOutputFrames,
            trimStartFrame: trimStart, trimEndFrame: trimEnd);
        return stream.ToArray();
    }

    private static int FrameCountOf(byte[] wavBytes) => (wavBytes.Length - 44) / 2;

    private static short PcmAt(byte[] wavBytes, int frameIndex) =>
        BitConverter.ToInt16(wavBytes, 44 + frameIndex * 2);

    [Fact]
    public void DefaultTrim_MatchesNoTrim_ByteEqual()
    {
        var sample = MakeRampSample(100);
        var withDefault = Render(sample, trimStart: 0, trimEnd: 0);
        var withoutTrim = Render(sample);
        Assert.Equal(withoutTrim, withDefault);
    }

    [Fact]
    public void TrimRange_ProducesShorterOutput_StartingAtTrimStart()
    {
        var sample = MakeRampSample(100);
        var bytes = Render(sample, trimStart: 25, trimEnd: 75);

        Assert.Equal(50, FrameCountOf(bytes));

        // output[0] should equal source[25]; output[49] should equal source[74].
        // The exporter uses (short)(v * 32768f) truncation (not rounding), matching WavSampleExporter.
        var output0 = PcmAt(bytes, 0);
        var expected0 = (short)(25 / 10000.0f * 32768f);
        Assert.Equal(expected0, output0);

        var output49 = PcmAt(bytes, 49);
        var expected49 = (short)(74 / 10000.0f * 32768f);
        Assert.Equal(expected49, output49);
    }

    [Fact]
    public void TrimStartOnly_OutputRunsFromTrimStartToSourceEnd()
    {
        var sample = MakeRampSample(100);
        var bytes = Render(sample, trimStart: 25, trimEnd: 0);

        Assert.Equal(75, FrameCountOf(bytes));
        var output0 = PcmAt(bytes, 0);
        var expected0 = (short)(25 / 10000.0f * 32768f);
        Assert.Equal(expected0, output0);
    }

    [Fact]
    public void TrimEndOnly_OutputRunsFromZeroToTrimEnd()
    {
        var sample = MakeRampSample(100);
        var bytes = Render(sample, trimStart: 0, trimEnd: 50);

        Assert.Equal(50, FrameCountOf(bytes));
    }

    [Fact]
    public void TrimWithLoop_LoopBoundsInterpretedWithinTrimmedRange()
    {
        // Source: 0..99. Trim [20, 80) gives an effective length of 60 frames.
        // Loop [10, 40) within the trimmed range → loop region [30, 60) in source space.
        // After the first 40 trimmed frames (output frames 0..39), output cycles through
        // trimmed indices [10, 40) = source [30, 60).
        var sample = MakeRampSample(100);
        var bytes = Render(
            sample,
            trimStart: 20,
            trimEnd: 80,
            loopEnabled: true,
            loopStart: 10,
            loopEnd: 40,
            maxOutputFrames: 100);

        // First trimmed segment ends at output frame 40 (trimmed index 40, source 60).
        Assert.Equal(100, FrameCountOf(bytes));

        // output[0] = source[20]
        // The exporter uses (short)(v * 32768f) truncation (not rounding), matching WavSampleExporter.
        var expected0 = (short)(20 / 10000.0f * 32768f);
        Assert.Equal(expected0, PcmAt(bytes, 0));

        // output[40] should wrap to trimmed[10] = source[30]
        var expected40 = (short)(30 / 10000.0f * 32768f);
        Assert.Equal(expected40, PcmAt(bytes, 40));
    }

    [Fact]
    public void NegativeTrimStart_Throws()
    {
        var sample = MakeRampSample(100);
        Assert.Throws<ArgumentException>(() => Render(sample, trimStart: -1));
    }

    [Fact]
    public void NegativeTrimEnd_Throws()
    {
        var sample = MakeRampSample(100);
        Assert.Throws<ArgumentException>(() => Render(sample, trimEnd: -1));
    }

    [Fact]
    public void TrimEndAtOrBeforeTrimStart_Throws()
    {
        var sample = MakeRampSample(100);
        Assert.Throws<ArgumentException>(() => Render(sample, trimStart: 50, trimEnd: 50));
        Assert.Throws<ArgumentException>(() => Render(sample, trimStart: 50, trimEnd: 40));
    }

    [Fact]
    public void TrimBeyondSourceLength_ClampsGracefully()
    {
        var sample = MakeRampSample(100);
        var bytes = Render(sample, trimStart: 50, trimEnd: 999);

        // trimEnd clamps to 100 → effective length 50 → 50 output frames.
        Assert.Equal(50, FrameCountOf(bytes));
    }
}
