using SynthSharp.Core.Audio;

namespace SynthSharp.Core.Tests;

public class SampleTests
{
    // Helper that produces a valid SampleMetadata for the given channel count and frame count.
    private static SampleMetadata MakeMetadata(int channelCount = 1, int frameCount = 10) =>
        new(
            Name: "test",
            ChannelCount: channelCount,
            SampleRateHz: 44100,
            FrameCount: frameCount,
            Duration: TimeSpan.FromSeconds(frameCount / 44100.0),
            SourceBitsPerSample: 16,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);

    // Helper that produces matching channel arrays.
    private static float[][] MakeChannels(int channelCount, int frameCount) =>
        Enumerable.Range(0, channelCount)
            .Select(_ => new float[frameCount])
            .ToArray();

    [Fact]
    public void Constructor_AcceptsMatchingMetadataAndChannels_Mono()
    {
        var metadata = MakeMetadata(channelCount: 1, frameCount: 10);
        var channels = MakeChannels(1, 10);

        var sample = new Sample(metadata, channels);

        Assert.Same(metadata, sample.Metadata);
        Assert.Single(sample.Channels);
        Assert.Equal(10, sample.Channels[0].Length);
    }

    [Fact]
    public void Constructor_AcceptsMatchingMetadataAndChannels_Stereo()
    {
        var metadata = MakeMetadata(channelCount: 2, frameCount: 50);
        var channels = MakeChannels(2, 50);

        var sample = new Sample(metadata, channels);

        Assert.Equal(2, sample.Channels.Count);
        Assert.Equal(50, sample.Channels[0].Length);
        Assert.Equal(50, sample.Channels[1].Length);
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenChannelCountMismatch()
    {
        var metadata = MakeMetadata(channelCount: 1, frameCount: 10);

        // Pass 2 channel arrays for a mono metadata.
        var channels = MakeChannels(2, 10);

        var ex = Assert.Throws<ArgumentException>(() => new Sample(metadata, channels));
        Assert.Contains("Channel count", ex.Message);
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenChannelArrayLengthMismatch()
    {
        var metadata = MakeMetadata(channelCount: 1, frameCount: 10);

        // Channel array has wrong frame count.
        var channels = new float[][] { new float[5] };

        var ex = Assert.Throws<ArgumentException>(() => new Sample(metadata, channels));
        Assert.Contains("length", ex.Message);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenMetadataIsNull()
    {
        var channels = MakeChannels(1, 10);

        Assert.Throws<ArgumentNullException>(() => new Sample(null!, channels));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenChannelsIsNull()
    {
        var metadata = MakeMetadata(channelCount: 1, frameCount: 10);

        Assert.Throws<ArgumentNullException>(() => new Sample(metadata, null!));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenChannelEntryIsNull()
    {
        var metadata = MakeMetadata(channelCount: 1, frameCount: 10);

        // One channel array, but it's null.
        var channels = new float[][] { null! };

        Assert.Throws<ArgumentNullException>(() => new Sample(metadata, channels));
    }
}
