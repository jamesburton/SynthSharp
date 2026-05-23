namespace SynthSharp.Core.Audio;

/// <summary>An in-memory PCM sample stored as planar float32 channels in the range [-1.0, 1.0].</summary>
public sealed class Sample
{
    /// <summary>Metadata describing this sample.</summary>
    public SampleMetadata Metadata { get; }

    /// <summary>Planar audio data — one float[] per channel. Index order is [channel][frame].</summary>
    public IReadOnlyList<float[]> Channels { get; }

    /// <summary>Initializes a new sample, validating that the channel arrays match the metadata.</summary>
    /// <param name="metadata">Metadata describing the sample.</param>
    /// <param name="channels">Planar audio data — one float[] per channel, each of length metadata.FrameCount.</param>
    /// <exception cref="ArgumentException">Thrown when channels.Count != metadata.ChannelCount or any channel array length != metadata.FrameCount.</exception>
    public Sample(SampleMetadata metadata, IReadOnlyList<float[]> channels)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(channels);

        if (channels.Count != metadata.ChannelCount)
        {
            throw new ArgumentException(
                $"Channel count {channels.Count} does not match metadata.ChannelCount {metadata.ChannelCount}.",
                nameof(channels));
        }

        for (var i = 0; i < channels.Count; i++)
        {
            ArgumentNullException.ThrowIfNull(channels[i]);
            if (channels[i].Length != metadata.FrameCount)
            {
                throw new ArgumentException(
                    $"Channel {i} length {channels[i].Length} does not match metadata.FrameCount {metadata.FrameCount}.",
                    nameof(channels));
            }
        }

        Metadata = metadata;
        Channels = channels;
    }
}
