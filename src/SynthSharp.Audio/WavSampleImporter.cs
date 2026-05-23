using System.Buffers.Binary;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Persistence;

namespace SynthSharp.Audio;

/// <summary>Imports PCM16 mono or stereo WAV files into a <see cref="Sample"/> with float32 internal representation.</summary>
public sealed class WavSampleImporter : ISampleImporter
{
    /// <inheritdoc/>
    public Sample Import(Stream source, string? sourcePath = null, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var reader = new BinaryReader(source, System.Text.Encoding.ASCII, leaveOpen: true);

        // 1. Verify RIFF/WAVE header.
        var riff = reader.ReadBytes(4);
        if (riff.Length < 4 || !riff.SequenceEqual("RIFF"u8.ToArray()))
        {
            throw new InvalidDataException("Not a RIFF file (missing 'RIFF' magic).");
        }

        _ = reader.ReadUInt32(); // riff chunk size — not validated against stream length

        var wave = reader.ReadBytes(4);
        if (wave.Length < 4 || !wave.SequenceEqual("WAVE"u8.ToArray()))
        {
            throw new InvalidDataException("Not a WAVE file (missing 'WAVE' identifier).");
        }

        // 2. Locate the 'fmt ' chunk (it may not be the first chunk after WAVE in pathological files,
        //    but in standard WAV it is — still, scan for safety).
        var (formatCode, channelCount, sampleRate, bitsPerSample) = ReadFmtChunk(reader);

        if (formatCode != 1)
        {
            throw new InvalidDataException($"Unsupported WAV format code {formatCode}; only PCM (1) is supported.");
        }

        if (channelCount is not (1 or 2))
        {
            throw new InvalidDataException($"Unsupported channel count {channelCount}; only mono (1) and stereo (2) are supported.");
        }

        if (bitsPerSample != 16)
        {
            throw new InvalidDataException($"Unsupported bit depth {bitsPerSample}; only PCM16 is supported.");
        }

        // 3. Locate the 'data' chunk, skipping any intervening chunks.
        var dataSize = SeekToDataChunk(reader);

        // 4. Read the PCM payload.
        var bytesPerFrame = channelCount * 2;
        var frameCount = dataSize / bytesPerFrame;
        var pcm = reader.ReadBytes(dataSize);
        if (pcm.Length < dataSize)
        {
            throw new InvalidDataException("WAV data chunk is truncated.");
        }

        // 5. Deinterleave into planar float32.
        var channels = new float[channelCount][];
        for (var c = 0; c < channelCount; c++)
        {
            channels[c] = new float[frameCount];
        }

        for (var f = 0; f < frameCount; f++)
        {
            var frameStart = f * bytesPerFrame;
            for (var c = 0; c < channelCount; c++)
            {
                var sampleBytes = pcm.AsSpan(frameStart + (c * 2), 2);
                var pcm16 = BinaryPrimitives.ReadInt16LittleEndian(sampleBytes);

                // Divide by 32768.0f (not 32767) so that short.MinValue (-32768) maps to exactly -1.0f.
                channels[c][f] = pcm16 / 32768.0f;
            }
        }

        // 6. Build metadata + sample.
        var resolvedName = ResolveName(name, sourcePath);
        var duration = TimeSpan.FromSeconds(frameCount / (double)sampleRate);
        var metadata = new SampleMetadata(
            Name: resolvedName,
            ChannelCount: channelCount,
            SampleRateHz: sampleRate,
            FrameCount: frameCount,
            Duration: duration,
            SourceBitsPerSample: bitsPerSample,
            SourcePath: sourcePath,
            ImportedAt: DateTimeOffset.UtcNow);

        return new Sample(metadata, channels);
    }

    private static (int FormatCode, int ChannelCount, int SampleRate, int BitsPerSample) ReadFmtChunk(BinaryReader reader)
    {
        // Walk chunks until we find "fmt ".
        while (true)
        {
            var chunkId = reader.ReadBytes(4);
            if (chunkId.Length < 4)
            {
                throw new InvalidDataException("Reached end of stream before locating 'fmt ' chunk.");
            }

            var chunkSize = (int)reader.ReadUInt32();

            if (chunkId.SequenceEqual("fmt "u8.ToArray()))
            {
                if (chunkSize < 16)
                {
                    throw new InvalidDataException($"'fmt ' chunk size {chunkSize} is below the minimum 16 bytes.");
                }

                var formatCode = reader.ReadUInt16();
                var channels = reader.ReadUInt16();
                var sampleRate = (int)reader.ReadUInt32();
                _ = reader.ReadUInt32(); // byte rate — not validated
                _ = reader.ReadUInt16(); // block align — not validated
                var bitsPerSample = reader.ReadUInt16();

                // Skip any fmt extension bytes beyond the canonical 16.
                const int consumed = 16;
                if (chunkSize > consumed)
                {
                    reader.BaseStream.Seek(chunkSize - consumed, SeekOrigin.Current);
                }

                return (formatCode, channels, sampleRate, bitsPerSample);
            }

            // Not 'fmt ' — skip this chunk.
            reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
        }
    }

    private static int SeekToDataChunk(BinaryReader reader)
    {
        while (true)
        {
            var chunkId = reader.ReadBytes(4);
            if (chunkId.Length < 4)
            {
                throw new InvalidDataException("Reached end of stream before locating 'data' chunk.");
            }

            var chunkSize = (int)reader.ReadUInt32();

            if (chunkId.SequenceEqual("data"u8.ToArray()))
            {
                return chunkSize;
            }

            // Skip non-data chunk.
            reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
        }
    }

    private static string ResolveName(string? name, string? sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            return Path.GetFileNameWithoutExtension(sourcePath);
        }

        return "imported-sample";
    }
}
