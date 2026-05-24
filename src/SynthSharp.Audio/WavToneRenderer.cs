using System.Buffers.Binary;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio;

public static class WavToneRenderer
{
    private const int SampleRate = 44100;
    private const short BitsPerSample = 16;
    private const short Channels = 1;

    /// <summary>
    /// Renders a mono PCM-16 WAV stream for the given waveform, frequency, duration, and envelope.
    /// </summary>
    /// <param name="waveform">The waveform shape to synthesise.</param>
    /// <param name="frequencyHz">Fundamental frequency in Hz.</param>
    /// <param name="duration">Duration of the rendered audio.</param>
    /// <param name="envelope">ADSR envelope applied as an amplitude curve.</param>
    /// <param name="filter">
    /// Optional per-pad filter applied sample-by-sample after envelope shaping.
    /// Pass <see langword="null"/> or <see cref="FilterSettings.Off"/> for bypass.
    /// </param>
    /// <returns>A read-only <see cref="MemoryStream"/> at position 0 containing the WAV bytes.</returns>
    public static MemoryStream RenderMonoPcm16(
        WaveformType waveform,
        double frequencyHz,
        TimeSpan duration,
        Envelope envelope,
        FilterSettings? filter = null)
    {
        var sampleCount = Math.Max(1, (int)(SampleRate * duration.TotalSeconds));
        var byteCount = sampleCount * sizeof(short);
        var totalLength = 44 + byteCount;
        var bytes = new byte[totalLength];

        WriteWaveHeader(bytes, byteCount);

        var attackSamples = (int)(SampleRate * envelope.AttackSeconds);
        var decaySamples = (int)(SampleRate * envelope.DecaySeconds);
        var releaseSamples = (int)(SampleRate * envelope.ReleaseSeconds);
        var sustainStart = attackSamples + decaySamples;
        var sustainEnd = Math.Max(sustainStart, sampleCount - releaseSamples);

        // Construct the filter once up front; null means bypass.
        var biquad = filter is not null ? AudioFilters.Create(filter, SampleRate) : null;

        for (var i = 0; i < sampleCount; i++)
        {
            var phase = 2d * Math.PI * frequencyHz * (i / (double)SampleRate);
            var amplitude = EnvelopeAmplitude(i, sampleCount, attackSamples, decaySamples, sustainStart, sustainEnd, releaseSamples, envelope.SustainLevel);
            var rendered = WaveformSampleGenerator.NextSample(waveform, phase) * amplitude;

            // Apply filter when present; keep the null path lossless (no float cast).
            var filtered = biquad is not null ? (double)biquad.Process((float)rendered) : rendered;

            var pcm = (short)Math.Clamp(filtered * short.MaxValue, short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(44 + (i * 2), 2), pcm);
        }

        return new MemoryStream(bytes, writable: false);
    }

    private static void WriteWaveHeader(Span<byte> buffer, int dataSize)
    {
        "RIFF"u8.CopyTo(buffer[..4]);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..8], 36 + dataSize);
        "WAVE"u8.CopyTo(buffer[8..12]);
        "fmt "u8.CopyTo(buffer[12..16]);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[16..20], 16);
        BinaryPrimitives.WriteInt16LittleEndian(buffer[20..22], 1);
        BinaryPrimitives.WriteInt16LittleEndian(buffer[22..24], Channels);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[24..28], SampleRate);
        var byteRate = SampleRate * Channels * (BitsPerSample / 8);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[28..32], byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(buffer[32..34], (short)(Channels * (BitsPerSample / 8)));
        BinaryPrimitives.WriteInt16LittleEndian(buffer[34..36], BitsPerSample);
        "data"u8.CopyTo(buffer[36..40]);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[40..44], dataSize);
    }

    private static double EnvelopeAmplitude(
        int sampleIndex,
        int sampleCount,
        int attackSamples,
        int decaySamples,
        int sustainStart,
        int sustainEnd,
        int releaseSamples,
        double sustainLevel)
    {
        if (attackSamples > 0 && sampleIndex < attackSamples)
        {
            return sampleIndex / (double)attackSamples;
        }

        if (decaySamples > 0 && sampleIndex < sustainStart)
        {
            var decayProgress = (sampleIndex - attackSamples) / (double)Math.Max(1, decaySamples);
            return 1d - ((1d - sustainLevel) * decayProgress);
        }

        if (sampleIndex < sustainEnd)
        {
            return sustainLevel;
        }

        if (releaseSamples <= 0)
        {
            return sustainLevel;
        }

        var releaseProgress = (sampleIndex - sustainEnd) / (double)Math.Max(1, releaseSamples);
        return sustainLevel * (1d - Math.Clamp(releaseProgress, 0d, 1d));
    }
}
