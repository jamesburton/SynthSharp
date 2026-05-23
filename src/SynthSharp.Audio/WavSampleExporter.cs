using System.Buffers.Binary;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Persistence;

namespace SynthSharp.Audio;

/// <summary>Exports an in-memory <see cref="Sample"/> as a PCM16 little-endian WAV byte stream.</summary>
public sealed class WavSampleExporter : ISampleExporter
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sample"/> or <paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="sample"/> has an unsupported channel count (not 1 or 2),
    /// a non-positive sample rate, or a negative frame count.
    /// </exception>
    public void Export(Sample sample, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(destination);

        var metadata = sample.Metadata;
        if (metadata.ChannelCount is not (1 or 2))
        {
            throw new ArgumentException(
                $"Unsupported channel count {metadata.ChannelCount}; WAV exporter supports mono (1) and stereo (2) only.",
                nameof(sample));
        }

        if (metadata.SampleRateHz <= 0)
        {
            throw new ArgumentException(
                $"Sample rate must be positive (got {metadata.SampleRateHz}).",
                nameof(sample));
        }

        if (metadata.FrameCount < 0)
        {
            throw new ArgumentException(
                $"Frame count cannot be negative (got {metadata.FrameCount}).",
                nameof(sample));
        }

        var channelCount = metadata.ChannelCount;
        var frameCount = metadata.FrameCount;
        var bytesPerFrame = channelCount * 2;
        var dataSize = frameCount * bytesPerFrame;

        // Write the 44-byte WAV header.
        Span<byte> header = stackalloc byte[44];
        WriteHeader(header, channelCount, metadata.SampleRateHz, dataSize);
        destination.Write(header);

        // Interleave channels and convert float32 to PCM16.
        // For stereo, each frame emits 4 bytes (L sample then R sample).
        Span<byte> frameBuffer = stackalloc byte[4]; // max 2 channels * 2 bytes
        for (var f = 0; f < frameCount; f++)
        {
            for (var c = 0; c < channelCount; c++)
            {
                var v = sample.Channels[c][f];

                // Multiply by 32768 so that -1.0 maps exactly to short.MinValue (-32768)
                // and +1.0 maps to 32768, which is clamped to short.MaxValue (32767).
                var scaled = v * 32768.0f;
                var clamped = Math.Clamp(scaled, short.MinValue, short.MaxValue);
                var pcm = (short)clamped;
                BinaryPrimitives.WriteInt16LittleEndian(frameBuffer.Slice(c * 2, 2), pcm);
            }

            destination.Write(frameBuffer[..bytesPerFrame]);
        }
    }

    private static void WriteHeader(Span<byte> buffer, int channelCount, int sampleRate, int dataSize)
    {
        const int bitsPerSample = 16;
        const int bytesPerSample = bitsPerSample / 8;

        "RIFF"u8.CopyTo(buffer[..4]);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..8], 36 + dataSize);
        "WAVE"u8.CopyTo(buffer[8..12]);
        "fmt "u8.CopyTo(buffer[12..16]);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[16..20], 16);  // fmt chunk size = 16 for PCM
        BinaryPrimitives.WriteInt16LittleEndian(buffer[20..22], 1);   // format code: PCM
        BinaryPrimitives.WriteInt16LittleEndian(buffer[22..24], (short)channelCount);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[24..28], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[28..32], sampleRate * channelCount * bytesPerSample); // byte rate
        BinaryPrimitives.WriteInt16LittleEndian(buffer[32..34], (short)(channelCount * bytesPerSample));     // block align
        BinaryPrimitives.WriteInt16LittleEndian(buffer[34..36], bitsPerSample);
        "data"u8.CopyTo(buffer[36..40]);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[40..44], dataSize);
    }
}
