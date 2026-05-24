using System.Buffers.Binary;
using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests;

public class WavSampleImporterTests
{
    private readonly WavSampleImporter _importer = new();

    // ---------------------------------------------------------------------------
    // WAV byte-stream builder
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal, well-formed WAV byte array for testing.
    /// </summary>
    /// <param name="pcmSamples">
    /// Interleaved PCM samples (for stereo: L0, R0, L1, R1, …).
    /// Length must be divisible by <paramref name="channelCount"/>.
    /// </param>
    /// <param name="channelCount">1 = mono, 2 = stereo.</param>
    /// <param name="sampleRate">Samples per second.</param>
    /// <param name="bitsPerSample">Bit depth written into the fmt chunk.</param>
    /// <param name="formatCode">PCM format code (1 = PCM, other values for rejection tests).</param>
    /// <param name="extraChunksBeforeData">
    /// Optional additional chunks inserted between fmt and data.
    /// Each tuple is (4-char ASCII id, payload bytes).
    /// </param>
    /// <param name="truncateDataBy">
    /// Bytes to omit from the end of the actual data payload (for truncation tests).
    /// The header still declares the full size.
    /// </param>
    private static byte[] BuildWavBytes(
        short[] pcmSamples,
        int channelCount,
        int sampleRate,
        int bitsPerSample = 16,
        ushort formatCode = 1,
        IEnumerable<(string id, byte[] data)>? extraChunksBeforeData = null,
        int truncateDataBy = 0)
    {
        var extras = extraChunksBeforeData?.ToList() ?? [];
        var bytesPerSample = bitsPerSample / 8;
        var dataPayloadBytes = pcmSamples.Length * bytesPerSample;

        // Compute total extra-chunk bytes (each has 8-byte header).
        var extraBytes = extras.Sum(e => 8 + e.data.Length);

        // Total: RIFF(12) + fmt(24) + extras + data(8 + declared size).
        // Declared data size is full, but we may emit fewer bytes (truncation test).
        var totalSize = 12 + 24 + extraBytes + 8 + dataPayloadBytes;
        var buf = new byte[totalSize - truncateDataBy];
        var span = buf.AsSpan();
        var pos = 0;

        // RIFF header
        WriteAscii(span, ref pos, "RIFF");
        WriteInt32LE(span, ref pos, totalSize - 8); // RIFF chunk size = file minus first 8 bytes
        WriteAscii(span, ref pos, "WAVE");

        // fmt chunk (always 16 bytes for canonical PCM)
        WriteAscii(span, ref pos, "fmt ");
        WriteInt32LE(span, ref pos, 16);
        WriteInt16LE(span, ref pos, (short)formatCode);
        WriteInt16LE(span, ref pos, (short)channelCount);
        WriteInt32LE(span, ref pos, sampleRate);
        WriteInt32LE(span, ref pos, sampleRate * channelCount * bytesPerSample); // byte rate
        WriteInt16LE(span, ref pos, (short)(channelCount * bytesPerSample)); // block align
        WriteInt16LE(span, ref pos, (short)bitsPerSample);

        // Extra chunks
        foreach (var (id, data) in extras)
        {
            WriteAscii(span, ref pos, id);
            WriteInt32LE(span, ref pos, data.Length);
            data.AsSpan().CopyTo(span[pos..]);
            pos += data.Length;
        }

        // data chunk header — declare the full size even if we truncate the payload.
        WriteAscii(span, ref pos, "data");
        WriteInt32LE(span, ref pos, dataPayloadBytes);

        // PCM payload (may be truncated).
        var remaining = buf.Length - pos;
        for (var i = 0; i < pcmSamples.Length && remaining >= 2; i++, remaining -= 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span[pos..], pcmSamples[i]);
            pos += 2;
        }

        return buf;
    }

    /// <summary>Builds WAV bytes with a fmt chunk whose declared size is larger than 16 (cbSize extension).</summary>
    private static byte[] BuildWavWithExtendedFmt(short[] pcmSamples, int channelCount, int sampleRate, byte[] fmtExtension)
    {
        var fmtSize = 16 + fmtExtension.Length;
        var dataPayloadBytes = pcmSamples.Length * 2;
        var totalSize = 12 + 8 + fmtSize + 8 + dataPayloadBytes;
        var buf = new byte[totalSize];
        var span = buf.AsSpan();
        var pos = 0;

        WriteAscii(span, ref pos, "RIFF");
        WriteInt32LE(span, ref pos, totalSize - 8);
        WriteAscii(span, ref pos, "WAVE");

        WriteAscii(span, ref pos, "fmt ");
        WriteInt32LE(span, ref pos, fmtSize);
        WriteInt16LE(span, ref pos, 1); // PCM
        WriteInt16LE(span, ref pos, (short)channelCount);
        WriteInt32LE(span, ref pos, sampleRate);
        WriteInt32LE(span, ref pos, sampleRate * channelCount * 2);
        WriteInt16LE(span, ref pos, (short)(channelCount * 2));
        WriteInt16LE(span, ref pos, 16); // bitsPerSample
        fmtExtension.AsSpan().CopyTo(span[pos..]);
        pos += fmtExtension.Length;

        WriteAscii(span, ref pos, "data");
        WriteInt32LE(span, ref pos, dataPayloadBytes);
        for (var i = 0; i < pcmSamples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span[pos..], pcmSamples[i]);
            pos += 2;
        }

        return buf;
    }

    private static void WriteAscii(Span<byte> buf, ref int pos, string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            buf[pos + i] = (byte)s[i];
        }

        pos += s.Length;
    }

    private static void WriteInt32LE(Span<byte> buf, ref int pos, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(buf[pos..], value);
        pos += 4;
    }

    private static void WriteInt16LE(Span<byte> buf, ref int pos, short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(buf[pos..], value);
        pos += 2;
    }

    // ---------------------------------------------------------------------------
    // Happy-path tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Import_ValidMonoPcm16_ReturnsCorrectMetadata()
    {
        var pcm = Enumerable.Range(0, 100).Select(i => (short)i).ToArray();
        var bytes = BuildWavBytes(pcm, channelCount: 1, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        var sample = _importer.Import(stream);

        Assert.Equal(1, sample.Metadata.ChannelCount);
        Assert.Equal(44100, sample.Metadata.SampleRateHz);
        Assert.Equal(100, sample.Metadata.FrameCount);
        Assert.Equal(16, sample.Metadata.SourceBitsPerSample);
        Assert.InRange(sample.Metadata.Duration.TotalSeconds, 100.0 / 44100 - 0.0001, 100.0 / 44100 + 0.0001);
        Assert.InRange((DateTimeOffset.UtcNow - sample.Metadata.ImportedAt).TotalSeconds, 0, 5);
        Assert.Single(sample.Channels);
        Assert.Equal(100, sample.Channels[0].Length);
    }

    [Fact]
    public void Import_ValidStereoPcm16_ReturnsTwoChannelsOfCorrectLength()
    {
        // 50 stereo frames = 100 interleaved samples (L0 R0 L1 R1 …)
        var pcm = Enumerable.Range(0, 100).Select(i => (short)i).ToArray();
        var bytes = BuildWavBytes(pcm, channelCount: 2, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        var sample = _importer.Import(stream);

        Assert.Equal(2, sample.Metadata.ChannelCount);
        Assert.Equal(50, sample.Metadata.FrameCount);
        Assert.Equal(2, sample.Channels.Count);
        Assert.Equal(50, sample.Channels[0].Length);
        Assert.Equal(50, sample.Channels[1].Length);
    }

    [Fact]
    public void Import_FloatValuesAreInExpectedRange()
    {
        // Three samples: min, zero, max.
        var pcm = new short[] { short.MinValue, 0, short.MaxValue };
        var bytes = BuildWavBytes(pcm, channelCount: 1, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        var sample = _importer.Import(stream);

        Assert.Equal(-1.0f, sample.Channels[0][0]);
        Assert.Equal(0.0f, sample.Channels[0][1]);

        // short.MaxValue (32767) / 32768 ≈ 0.99997
        Assert.InRange(sample.Channels[0][2], 0.9999f, 1.0f);
    }

    [Fact]
    public void Import_MostNegativePcmMapsToExactlyNegativeOne()
    {
        var pcm = new short[] { short.MinValue };
        var bytes = BuildWavBytes(pcm, channelCount: 1, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        var sample = _importer.Import(stream);

        Assert.Equal(-1.0f, sample.Channels[0][0]);
    }

    [Fact]
    public void Import_CustomNameOverrideIsRespected()
    {
        var pcm = new short[] { 0 };
        var bytes = BuildWavBytes(pcm, channelCount: 1, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        var sample = _importer.Import(stream, name: "drum-loop");

        Assert.Equal("drum-loop", sample.Metadata.Name);
    }

    [Fact]
    public void Import_NameFallsBackToFilenameWhenSourcePathGiven()
    {
        var pcm = new short[] { 0 };
        var bytes = BuildWavBytes(pcm, channelCount: 1, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        var sample = _importer.Import(stream, sourcePath: "C:/samples/snare.wav");

        Assert.Equal("snare", sample.Metadata.Name);
        Assert.Equal("C:/samples/snare.wav", sample.Metadata.SourcePath);
    }

    [Fact]
    public void Import_NameFallsBackToImportedSampleWhenNoPathAndNoName()
    {
        var pcm = new short[] { 0 };
        var bytes = BuildWavBytes(pcm, channelCount: 1, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        var sample = _importer.Import(stream);

        Assert.Equal("imported-sample", sample.Metadata.Name);
        Assert.Null(sample.Metadata.SourcePath);
    }

    [Fact]
    public void Import_ImportedAtIsRecentUtcTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var pcm = new short[] { 0 };
        var bytes = BuildWavBytes(pcm, channelCount: 1, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        var sample = _importer.Import(stream);

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(sample.Metadata.ImportedAt, before, after);
    }

    [Fact]
    public void Import_SourcePathFlowsThroughToMetadata()
    {
        var pcm = new short[] { 0 };
        var bytes = BuildWavBytes(pcm, channelCount: 1, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        var sample = _importer.Import(stream, sourcePath: "D:/sounds/kick.wav", name: "kick");

        Assert.Equal("D:/sounds/kick.wav", sample.Metadata.SourcePath);
    }

    [Fact]
    public void Import_SkipsUnknownChunksBeforeData()
    {
        // LIST chunk with 4 bytes of payload between fmt and data.
        var listPayload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var extras = new (string id, byte[] data)[] { ("LIST", listPayload) };

        var pcm = new short[] { 100, 200, 300 };
        var bytes = BuildWavBytes(pcm, channelCount: 1, sampleRate: 44100, extraChunksBeforeData: extras);
        using var stream = new MemoryStream(bytes);

        // Must not throw; must find and decode the data chunk correctly.
        var sample = _importer.Import(stream);

        Assert.Equal(3, sample.Metadata.FrameCount);
    }

    [Fact]
    public void Import_ToleratesExtendedFmtChunk()
    {
        // fmt chunk with 2 extra extension bytes (cbSize = 0, a common extension).
        var fmtExtension = new byte[] { 0x00, 0x00 };
        var pcm = new short[] { 500, 1000 };
        var bytes = BuildWavWithExtendedFmt(pcm, channelCount: 1, sampleRate: 22050, fmtExtension);
        using var stream = new MemoryStream(bytes);

        var sample = _importer.Import(stream);

        Assert.Equal(2, sample.Metadata.FrameCount);
        Assert.Equal(22050, sample.Metadata.SampleRateHz);
    }

    // ---------------------------------------------------------------------------
    // Rejection tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Import_RejectsNonRiffHeader()
    {
        var bytes = BuildWavBytes(new short[] { 0 }, channelCount: 1, sampleRate: 44100);

        // Overwrite RIFF magic with "JUNK".
        bytes[0] = (byte)'J'; bytes[1] = (byte)'U'; bytes[2] = (byte)'N'; bytes[3] = (byte)'K';

        using var stream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => _importer.Import(stream));
    }

    [Fact]
    public void Import_RejectsNonWaveMarker()
    {
        var bytes = BuildWavBytes(new short[] { 0 }, channelCount: 1, sampleRate: 44100);

        // Overwrite "WAVE" at offset 8 with "AVI ".
        bytes[8] = (byte)'A'; bytes[9] = (byte)'V'; bytes[10] = (byte)'I'; bytes[11] = (byte)' ';

        using var stream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => _importer.Import(stream));
    }

    [Theory]
    [InlineData(3)]   // IEEE float
    [InlineData(6)]   // A-law
    [InlineData(7)]   // μ-law
    public void Import_RejectsNonPcmFormatCode(ushort formatCode)
    {
        var bytes = BuildWavBytes(new short[] { 0 }, channelCount: 1, sampleRate: 44100, formatCode: formatCode);
        using var stream = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => _importer.Import(stream));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void Import_RejectsUnsupportedChannelCount(int channelCount)
    {
        var bytes = BuildWavBytes(new short[] { 0 }, channelCount: channelCount, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => _importer.Import(stream));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(24)]
    [InlineData(32)]
    public void Import_RejectsUnsupportedBitDepth(int bitsPerSample)
    {
        var bytes = BuildWavBytes(new short[] { 0 }, channelCount: 1, sampleRate: 44100, bitsPerSample: bitsPerSample);
        using var stream = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => _importer.Import(stream));
    }

    [Fact]
    public void Import_RejectsTruncatedDataChunk()
    {
        // Declare 10 samples worth of data but only write 5.
        var pcm = Enumerable.Range(0, 10).Select(i => (short)i).ToArray();
        var bytes = BuildWavBytes(pcm, channelCount: 1, sampleRate: 44100, truncateDataBy: 10);
        using var stream = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => _importer.Import(stream));
    }

    [Fact]
    public void Import_SkipsUnknownChunkBeforeFmt()
    {
        // Insert a non-fmt, non-data chunk BEFORE the fmt chunk.
        // ReadFmtChunk must scan past it and still find the fmt chunk correctly.
        var infoPayload = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        var pcm = new short[] { 1000, 2000, 3000 };

        // BuildWavWithChunkBeforeFmt: manually construct RIFF / INFO / fmt / data.
        var bytes = BuildWavBytesWithPreFmtChunk("INFO", infoPayload, pcm, channelCount: 1, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        var sample = _importer.Import(stream);

        Assert.Equal(3, sample.Metadata.FrameCount);
        Assert.Equal(44100, sample.Metadata.SampleRateHz);
    }

    [Fact]
    public void Import_RejectsFmtChunkSmallerThan16Bytes()
    {
        // Build a WAV whose fmt chunk size is declared as 10 (below the 16-byte minimum).
        var bytes = BuildWavBytesWithSmallFmt(fmtSize: 10, channelCount: 1, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => _importer.Import(stream));
    }

    [Fact]
    public void Import_RejectsTruncatedBeforeFmt()
    {
        // Stream that has RIFF+WAVE header but ends immediately after — no chunks at all.
        // ReadFmtChunk will try to read a chunk ID, get < 4 bytes, and throw.
        var buf = new byte[12];
        "RIFF"u8.CopyTo(buf.AsSpan(0));
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), 4);  // tiny declared size
        "WAVE"u8.CopyTo(buf.AsSpan(8));
        using var stream = new MemoryStream(buf);

        Assert.Throws<InvalidDataException>(() => _importer.Import(stream));
    }

    [Fact]
    public void Import_RejectsTruncatedBeforeData()
    {
        // Stream that has RIFF+WAVE+fmt but no data chunk — stream ends after fmt.
        // SeekToDataChunk will try to read a chunk ID, get < 4 bytes, and throw.
        var bytes = BuildWavBytesNoDataChunk(channelCount: 1, sampleRate: 44100);
        using var stream = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => _importer.Import(stream));
    }

    // ---------------------------------------------------------------------------
    // Additional builder helpers for importer edge-case tests
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds a RIFF/WAVE stream that places an extra chunk with the given id and payload
    /// BEFORE the fmt chunk, then writes a valid fmt chunk and data chunk.
    /// </summary>
    private static byte[] BuildWavBytesWithPreFmtChunk(
        string preFmtChunkId,
        byte[] preFmtPayload,
        short[] pcmSamples,
        int channelCount,
        int sampleRate)
    {
        var dataPayloadBytes = pcmSamples.Length * 2;

        // Total: RIFF(12) + preFmt(8 + payload) + fmt(24) + data(8 + dataPayload).
        var totalSize = 12 + 8 + preFmtPayload.Length + 24 + 8 + dataPayloadBytes;
        var buf = new byte[totalSize];
        var span = buf.AsSpan();
        var pos = 0;

        WriteAscii(span, ref pos, "RIFF");
        WriteInt32LE(span, ref pos, totalSize - 8);
        WriteAscii(span, ref pos, "WAVE");

        // Pre-fmt unknown chunk.
        WriteAscii(span, ref pos, preFmtChunkId);
        WriteInt32LE(span, ref pos, preFmtPayload.Length);
        preFmtPayload.AsSpan().CopyTo(span[pos..]);
        pos += preFmtPayload.Length;

        // fmt chunk.
        WriteAscii(span, ref pos, "fmt ");
        WriteInt32LE(span, ref pos, 16);
        WriteInt16LE(span, ref pos, 1); // PCM
        WriteInt16LE(span, ref pos, (short)channelCount);
        WriteInt32LE(span, ref pos, sampleRate);
        WriteInt32LE(span, ref pos, sampleRate * channelCount * 2); // byte rate
        WriteInt16LE(span, ref pos, (short)(channelCount * 2));     // block align
        WriteInt16LE(span, ref pos, 16);                             // bits per sample

        // data chunk.
        WriteAscii(span, ref pos, "data");
        WriteInt32LE(span, ref pos, dataPayloadBytes);
        for (var i = 0; i < pcmSamples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span[pos..], pcmSamples[i]);
            pos += 2;
        }

        return buf;
    }

    /// <summary>Builds a RIFF/WAVE stream with a fmt chunk whose declared size is <paramref name="fmtSize"/> (< 16).</summary>
    private static byte[] BuildWavBytesWithSmallFmt(int fmtSize, int channelCount, int sampleRate)
    {
        // We still write canonical 16 bytes in the fmt payload but declare a smaller size.
        var totalSize = 12 + 8 + fmtSize;
        var buf = new byte[12 + 8 + 16]; // always write 16 fmt bytes so no buffer overrun
        var span = buf.AsSpan();
        var pos = 0;

        WriteAscii(span, ref pos, "RIFF");
        WriteInt32LE(span, ref pos, totalSize - 8);
        WriteAscii(span, ref pos, "WAVE");
        WriteAscii(span, ref pos, "fmt ");
        WriteInt32LE(span, ref pos, fmtSize); // declared smaller than 16
        WriteInt16LE(span, ref pos, 1);
        WriteInt16LE(span, ref pos, (short)channelCount);
        WriteInt32LE(span, ref pos, sampleRate);
        WriteInt32LE(span, ref pos, sampleRate * channelCount * 2);
        WriteInt16LE(span, ref pos, (short)(channelCount * 2));
        WriteInt16LE(span, ref pos, 16);

        return buf;
    }

    /// <summary>
    /// Builds a RIFF/WAVE stream that contains a valid fmt chunk but no data chunk —
    /// the stream ends immediately after the fmt chunk body.
    /// </summary>
    private static byte[] BuildWavBytesNoDataChunk(int channelCount, int sampleRate)
    {
        // RIFF(12) + fmt(24) — no data chunk at all.
        var totalSize = 12 + 24;
        var buf = new byte[totalSize];
        var span = buf.AsSpan();
        var pos = 0;

        WriteAscii(span, ref pos, "RIFF");
        WriteInt32LE(span, ref pos, totalSize - 8);
        WriteAscii(span, ref pos, "WAVE");
        WriteAscii(span, ref pos, "fmt ");
        WriteInt32LE(span, ref pos, 16);
        WriteInt16LE(span, ref pos, 1);
        WriteInt16LE(span, ref pos, (short)channelCount);
        WriteInt32LE(span, ref pos, sampleRate);
        WriteInt32LE(span, ref pos, sampleRate * channelCount * 2);
        WriteInt16LE(span, ref pos, (short)(channelCount * 2));
        WriteInt16LE(span, ref pos, 16);

        return buf;
    }
}
