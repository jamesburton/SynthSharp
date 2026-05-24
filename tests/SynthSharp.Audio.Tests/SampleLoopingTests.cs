using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests;

/// <summary>Unit tests for <see cref="SampleRenderer"/>'s loop-extension behaviour.</summary>
public sealed class SampleLoopingTests
{
    private static Sample MakeRampSample(int frameCount, int sampleRate = 44100)
    {
        // Build a deterministic mono sample where channel[c][i] == i / 10000.0f.
        // The pattern lets us verify which source frames appear in the loop output.
        var channel = new float[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            channel[i] = i / 10000.0f;
        }

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
        bool loopEnabled,
        int loopStartFrame = 0,
        int loopEndFrame = 0,
        int maxOutputFrames = 0)
    {
        var flatEnvelope = new Envelope(0, 0, 1, 0);
        using var stream = SampleRenderer.Render(
            sample,
            gain: 1.0,
            envelope: flatEnvelope,
            exporter: new WavSampleExporter(),
            loopEnabled: loopEnabled,
            loopStartFrame: loopStartFrame,
            loopEndFrame: loopEndFrame,
            maxOutputFrames: maxOutputFrames);
        return stream.ToArray();
    }

    private static int FrameCountOf(byte[] wavBytes, int channelCount = 1) =>
        (wavBytes.Length - 44) / (channelCount * 2);

    private static short PcmAt(byte[] wavBytes, int frameIndex, int channelIndex = 0, int channelCount = 1)
    {
        var offset = 44 + (frameIndex * channelCount + channelIndex) * 2;
        return BitConverter.ToInt16(wavBytes, offset);
    }

    [Fact]
    public void LoopDisabled_RendersNaturalLength()
    {
        var sample = MakeRampSample(1000);
        var bytes = Render(sample, loopEnabled: false, maxOutputFrames: 5000);

        Assert.Equal(1000, FrameCountOf(bytes));
    }

    [Fact]
    public void LoopEnabled_FullSampleRange_RendersToMaxOutputFrames()
    {
        var sample = MakeRampSample(1000);
        var bytes = Render(sample, loopEnabled: true, loopStartFrame: 0, loopEndFrame: 0, maxOutputFrames: 5000);

        Assert.Equal(5000, FrameCountOf(bytes));
    }

    [Fact]
    public void LoopEnabled_MidSampleRegion_CyclesThroughLoopRangeAfterFirstPass()
    {
        // Source: frames 0..999 with values 0, 0.0001, 0.0002, ..., 0.0999.
        // Loop region [200, 500). After frame 500 in the output, source index should cycle
        // through 200, 201, ..., 499, 200, 201, ...
        var sample = MakeRampSample(1000);
        var bytes = Render(
            sample,
            loopEnabled: true,
            loopStartFrame: 200,
            loopEndFrame: 500,
            maxOutputFrames: 2000);

        // Sanity: output has 2000 frames.
        Assert.Equal(2000, FrameCountOf(bytes));

        // First pass: output[0..499] == source[0..499].
        // After looping kicks in at output index 500: output[500] should equal source[200],
        // output[501] == source[201], etc.
        // Source value i is i / 10000.0f, converted to PCM16 by *32768 then clamping.
        // We compare via expected source index.

        // output frame 500 should match source frame 200.
        var output500 = PcmAt(bytes, 500);
        var source200Value = 200 / 10000.0f;
        var expected500 = (short)Math.Round(source200Value * 32768);
        Assert.Equal(expected500, output500);

        // output frame 600 should match source frame 300.
        var output600 = PcmAt(bytes, 600);
        var source300Value = 300 / 10000.0f;
        var expected600 = (short)Math.Round(source300Value * 32768);
        Assert.Equal(expected600, output600);

        // output frame 800 should cycle to source 200 again (500 + 300 == output 800; (800-500)%300 == 0, so source 200).
        var output800 = PcmAt(bytes, 800);
        Assert.Equal(expected500, output800);
    }

    [Fact]
    public void LoopEnabled_MaxOutputFramesEqualToSourceFrameCount_DoesNotExtend()
    {
        // When maxOutputFrames doesn't exceed the source's frame count, looping is a no-op.
        var sample = MakeRampSample(1000);
        var bytes = Render(sample, loopEnabled: true, loopStartFrame: 200, loopEndFrame: 500, maxOutputFrames: 1000);

        Assert.Equal(1000, FrameCountOf(bytes));
    }

    [Fact]
    public void LoopEnabled_NegativeLoopStart_Throws()
    {
        var sample = MakeRampSample(1000);
        Assert.Throws<ArgumentException>(() =>
            Render(sample, loopEnabled: true, loopStartFrame: -1, loopEndFrame: 500, maxOutputFrames: 2000));
    }

    [Fact]
    public void LoopEnabled_NegativeLoopEnd_Throws()
    {
        var sample = MakeRampSample(1000);
        Assert.Throws<ArgumentException>(() =>
            Render(sample, loopEnabled: true, loopStartFrame: 100, loopEndFrame: -1, maxOutputFrames: 2000));
    }

    [Fact]
    public void LoopEnabled_LoopEndAtOrBeforeLoopStart_Throws()
    {
        var sample = MakeRampSample(1000);
        Assert.Throws<ArgumentException>(() =>
            Render(sample, loopEnabled: true, loopStartFrame: 500, loopEndFrame: 500, maxOutputFrames: 2000));
        Assert.Throws<ArgumentException>(() =>
            Render(sample, loopEnabled: true, loopStartFrame: 500, loopEndFrame: 400, maxOutputFrames: 2000));
    }

    [Fact]
    public void LoopDisabled_InvalidLoopBounds_AreIgnored()
    {
        // When looping is disabled the loop fields should not be validated.
        var sample = MakeRampSample(1000);
        var bytes = Render(sample, loopEnabled: false, loopStartFrame: 500, loopEndFrame: 400, maxOutputFrames: 2000);

        Assert.Equal(1000, FrameCountOf(bytes));
    }
}
