using System.Buffers.Binary;
using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests;

public class WavSampleExporterTests
{
    private readonly WavSampleExporter _exporter = new();
    private readonly WavSampleImporter _importer = new();

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="Sample"/> with the given parameters and uniform float values per channel.
    /// </summary>
    private static Sample BuildSample(int channelCount, int frameCount, int sampleRateHz, float fillValue = 0.0f)
    {
        var duration = TimeSpan.FromSeconds(frameCount / (double)sampleRateHz);
        var metadata = new SampleMetadata(
            Name: "test",
            ChannelCount: channelCount,
            SampleRateHz: sampleRateHz,
            FrameCount: frameCount,
            Duration: duration,
            SourceBitsPerSample: 16,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);

        var channels = new float[channelCount][];
        for (var c = 0; c < channelCount; c++)
        {
            channels[c] = Enumerable.Repeat(fillValue, frameCount).ToArray();
        }

        return new Sample(metadata, channels);
    }

    /// <summary>
    /// Builds a <see cref="Sample"/> where each channel is populated by the provided per-channel value arrays.
    /// </summary>
    private static Sample BuildSampleWithValues(int sampleRateHz, float[][] channelValues)
    {
        var channelCount = channelValues.Length;
        var frameCount = channelValues[0].Length;
        var duration = TimeSpan.FromSeconds(frameCount / (double)sampleRateHz);
        var metadata = new SampleMetadata(
            Name: "test",
            ChannelCount: channelCount,
            SampleRateHz: sampleRateHz,
            FrameCount: frameCount,
            Duration: duration,
            SourceBitsPerSample: 16,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);

        return new Sample(metadata, channelValues);
    }

    /// <summary>
    /// Exports the sample to a new <see cref="MemoryStream"/> and rewinds it.
    /// </summary>
    private MemoryStream ExportToStream(Sample sample)
    {
        var ms = new MemoryStream();
        _exporter.Export(sample, ms);
        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Reads an int16 LE value from the exported byte array at a given offset.
    /// </summary>
    private static short ReadInt16At(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2));

    /// <summary>
    /// Reads an int32 LE value from the exported byte array at a given offset.
    /// </summary>
    private static int ReadInt32At(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));

    // ---------------------------------------------------------------------------
    // Header validation tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Export_ValidMonoSample_WritesCorrectHeader()
    {
        var sample = BuildSample(channelCount: 1, frameCount: 100, sampleRateHz: 44100);
        using var ms = ExportToStream(sample);
        var data = ms.ToArray();

        // RIFF marker
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(data, 0, 4));

        // WAVE marker
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(data, 8, 4));

        // fmt chunk marker (including trailing space)
        Assert.Equal("fmt ", System.Text.Encoding.ASCII.GetString(data, 12, 4));

        // fmt chunk size = 16
        Assert.Equal(16, ReadInt32At(data, 16));

        // format code = PCM (1)
        Assert.Equal(1, ReadInt16At(data, 20));

        // channel count = 1
        Assert.Equal(1, ReadInt16At(data, 22));

        // sample rate = 44100
        Assert.Equal(44100, ReadInt32At(data, 24));

        // bits per sample = 16
        Assert.Equal(16, ReadInt16At(data, 34));

        // data chunk marker
        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(data, 36, 4));
    }

    [Fact]
    public void Export_ValidStereoSample_WritesCorrectHeader()
    {
        var sample = BuildSample(channelCount: 2, frameCount: 50, sampleRateHz: 44100);
        using var ms = ExportToStream(sample);
        var data = ms.ToArray();

        // RIFF/WAVE/fmt/data markers
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(data, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(data, 8, 4));
        Assert.Equal("fmt ", System.Text.Encoding.ASCII.GetString(data, 12, 4));
        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(data, 36, 4));

        // format code = PCM (1)
        Assert.Equal(1, ReadInt16At(data, 20));

        // channel count = 2
        Assert.Equal(2, ReadInt16At(data, 22));

        // bits per sample = 16
        Assert.Equal(16, ReadInt16At(data, 34));

        // sample rate = 44100
        Assert.Equal(44100, ReadInt32At(data, 24));
    }

    [Fact]
    public void Export_PreservesSampleRateInHeader()
    {
        var sample = BuildSample(channelCount: 1, frameCount: 10, sampleRateHz: 48000);
        using var ms = ExportToStream(sample);
        var data = ms.ToArray();

        Assert.Equal(48000, ReadInt32At(data, 24));
    }

    [Fact]
    public void Export_DataSizeFieldIsCorrectByteCount()
    {
        // 100 mono frames * 1 channel * 2 bytes = 200 bytes of data
        var sample = BuildSample(channelCount: 1, frameCount: 100, sampleRateHz: 44100);
        using var ms = ExportToStream(sample);
        var data = ms.ToArray();

        var dataSize = ReadInt32At(data, 40);
        Assert.Equal(200, dataSize);

        // Total file size should be 44 (header) + 200 (data) = 244
        Assert.Equal(244, data.Length);
    }

    [Fact]
    public void Export_RiffChunkSizeIsFileSizeMinusEight()
    {
        // 50 stereo frames * 2 channels * 2 bytes = 200 bytes of data; total = 244; riff chunk = 236
        var sample = BuildSample(channelCount: 2, frameCount: 50, sampleRateHz: 44100);
        using var ms = ExportToStream(sample);
        var data = ms.ToArray();

        var riffChunkSize = ReadInt32At(data, 4);
        Assert.Equal(data.Length - 8, riffChunkSize);
    }

    // ---------------------------------------------------------------------------
    // Interleaving test
    // ---------------------------------------------------------------------------

    [Fact]
    public void Export_InterleavesStereoFrames()
    {
        // Left channel: 0.1, 0.2, 0.3  Right channel: -0.1, -0.2, -0.3
        var channelValues = new float[][]
        {
            [0.1f, 0.2f, 0.3f],
            [-0.1f, -0.2f, -0.3f]
        };
        var sample = BuildSampleWithValues(sampleRateHz: 44100, channelValues);
        using var ms = ExportToStream(sample);
        var data = ms.ToArray();

        // Data begins at offset 44; each frame is 4 bytes (L int16 + R int16).
        // Frame 0: L=0.1f, R=-0.1f
        var l0 = ReadInt16At(data, 44);
        var r0 = ReadInt16At(data, 46);

        // Frame 1: L=0.2f, R=-0.2f
        var l1 = ReadInt16At(data, 48);
        var r1 = ReadInt16At(data, 50);

        // Frame 2: L=0.3f, R=-0.3f
        var l2 = ReadInt16At(data, 52);
        var r2 = ReadInt16At(data, 54);

        // Left samples should be positive, right negative.
        Assert.True(l0 > 0, $"Expected L0 > 0, got {l0}");
        Assert.True(r0 < 0, $"Expected R0 < 0, got {r0}");
        Assert.True(l1 > l0, $"Expected L1 > L0, got L1={l1}, L0={l0}");
        Assert.True(r1 < r0, $"Expected R1 < R0, got R1={r1}, R0={r0}");
        Assert.True(l2 > l1, $"Expected L2 > L1, got L2={l2}, L1={l1}");
        Assert.True(r2 < r1, $"Expected R2 < R1, got R2={r2}, R1={r1}");

        // Symmetry: |L| should equal |R| for matching frames (within rounding).
        Assert.InRange(Math.Abs(l0 + r0), 0, 1);
        Assert.InRange(Math.Abs(l1 + r1), 0, 1);
        Assert.InRange(Math.Abs(l2 + r2), 0, 1);
    }

    // ---------------------------------------------------------------------------
    // Clipping tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Export_ClipsOutOfRangePositiveSampleToShortMax()
    {
        var channelValues = new float[][] { [2.0f] };
        var sample = BuildSampleWithValues(sampleRateHz: 44100, channelValues);
        using var ms = ExportToStream(sample);
        var data = ms.ToArray();

        var pcm = ReadInt16At(data, 44);
        Assert.Equal(short.MaxValue, pcm);
    }

    [Fact]
    public void Export_ClipsOutOfRangeNegativeSampleToShortMin()
    {
        var channelValues = new float[][] { [-2.0f] };
        var sample = BuildSampleWithValues(sampleRateHz: 44100, channelValues);
        using var ms = ExportToStream(sample);
        var data = ms.ToArray();

        var pcm = ReadInt16At(data, 44);
        Assert.Equal(short.MinValue, pcm);
    }

    // ---------------------------------------------------------------------------
    // Rejection tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Export_RejectsUnsupportedChannelCount()
    {
        // Construct a 3-channel sample — valid at the Sample level, rejected by the exporter.
        var channelValues = new float[][]
        {
            [0.1f, 0.2f],
            [0.3f, 0.4f],
            [0.5f, 0.6f]
        };
        var sample = BuildSampleWithValues(sampleRateHz: 44100, channelValues);
        using var ms = new MemoryStream();

        Assert.Throws<ArgumentException>(() => _exporter.Export(sample, ms));
    }

    [Fact]
    public void Export_ThrowsOnNullSample()
    {
        using var ms = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() => _exporter.Export(null!, ms));
    }

    [Fact]
    public void Export_ThrowsOnNullDestination()
    {
        var sample = BuildSample(channelCount: 1, frameCount: 1, sampleRateHz: 44100);
        Assert.Throws<ArgumentNullException>(() => _exporter.Export(sample, null!));
    }

    // ---------------------------------------------------------------------------
    // Round-trip tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Export_ThenImport_RoundTripsMono()
    {
        // 100 mono frames with distinct recognisable values.
        var frameCount = 100;
        var channelValues = new float[][]
        {
            Enumerable.Range(0, frameCount).Select(i => (float)i / frameCount * 0.9f).ToArray()
        };
        var original = BuildSampleWithValues(sampleRateHz: 44100, channelValues);

        using var ms = ExportToStream(original);
        var roundTripped = _importer.Import(ms);

        // Metadata round-trip.
        Assert.Equal(original.Metadata.ChannelCount, roundTripped.Metadata.ChannelCount);
        Assert.Equal(original.Metadata.SampleRateHz, roundTripped.Metadata.SampleRateHz);
        Assert.Equal(original.Metadata.FrameCount, roundTripped.Metadata.FrameCount);
        Assert.Equal(original.Metadata.Duration.TotalSeconds, roundTripped.Metadata.Duration.TotalSeconds, precision: 6);

        // Per-frame audio round-trip within PCM16 quantization tolerance.
        const float tolerance = 1.0f / 32768f;
        for (var f = 0; f < frameCount; f++)
        {
            Assert.InRange(
                roundTripped.Channels[0][f],
                original.Channels[0][f] - tolerance,
                original.Channels[0][f] + tolerance);
        }
    }

    [Fact]
    public void Export_ThenImport_RoundTripsStereo()
    {
        // 80 stereo frames: L ascending, R descending.
        var frameCount = 80;
        var channelValues = new float[][]
        {
            Enumerable.Range(0, frameCount).Select(i => (float)i / frameCount * 0.8f).ToArray(),
            Enumerable.Range(0, frameCount).Select(i => -(float)i / frameCount * 0.8f).ToArray()
        };
        var original = BuildSampleWithValues(sampleRateHz: 48000, channelValues);

        using var ms = ExportToStream(original);
        var roundTripped = _importer.Import(ms);

        // Metadata round-trip.
        Assert.Equal(original.Metadata.ChannelCount, roundTripped.Metadata.ChannelCount);
        Assert.Equal(original.Metadata.SampleRateHz, roundTripped.Metadata.SampleRateHz);
        Assert.Equal(original.Metadata.FrameCount, roundTripped.Metadata.FrameCount);

        // Per-frame audio round-trip for both channels within PCM16 quantization tolerance.
        const float tolerance = 1.0f / 32768f;
        for (var f = 0; f < frameCount; f++)
        {
            for (var c = 0; c < 2; c++)
            {
                Assert.InRange(
                    roundTripped.Channels[c][f],
                    original.Channels[c][f] - tolerance,
                    original.Channels[c][f] + tolerance);
            }
        }
    }
}
