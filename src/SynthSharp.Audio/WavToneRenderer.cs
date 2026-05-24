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
    /// <param name="lfo">
    /// Optional per-pad LFO applied during rendering. Pass <see langword="null"/> or
    /// <see cref="LfoSettings.Off"/> for bypass. <see cref="LfoTarget.None"/> produces
    /// output bit-identical to passing no LFO at all.
    /// </param>
    /// <returns>A read-only <see cref="MemoryStream"/> at position 0 containing the WAV bytes.</returns>
    public static MemoryStream RenderMonoPcm16(
        WaveformType waveform,
        double frequencyHz,
        TimeSpan duration,
        Envelope envelope,
        FilterSettings? filter = null,
        LfoSettings? lfo = null)
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

        // Determine active LFO target; null and Target.None both mean bypass.
        var lfoTarget = lfo?.Target ?? LfoTarget.None;

        // Construct the filter once up front; null means bypass.
        // For FilterCutoff modulation the filter is recreated every 256 samples (see below).
        var biquad = filter is not null ? AudioFilters.Create(filter, SampleRate) : null;

        // Phase accumulator used only for the Pitch LFO path; other paths use the
        // simpler per-sample formula to keep output bit-identical to the no-LFO baseline.
        var pitchPhase = 0d;

        for (var i = 0; i < sampleCount; i++)
        {
            double phase;
            if (lfoTarget == LfoTarget.Pitch)
            {
                // Incremental phase accumulator: frequency is modulated per-sample (~±6% at depth=1,
                // roughly ±1 semitone), so the phase must accumulate step-by-step rather than being
                // computed from i/sr directly.
                var timeSeconds = i / (double)SampleRate;
                var lfoValue = Lfo.EvaluateSine(lfo!.RateHz, timeSeconds, lfo.Depth);
                var modFreq = frequencyHz * (1 + lfoValue * 0.06);
                pitchPhase += 2d * Math.PI * modFreq / SampleRate;
                phase = pitchPhase;
            }
            else
            {
                // Original per-sample computation; preserves bit-identical output when LFO is off.
                phase = 2d * Math.PI * frequencyHz * (i / (double)SampleRate);
            }

            var amplitude = EnvelopeAmplitude(i, sampleCount, attackSamples, decaySamples, sustainStart, sustainEnd, releaseSamples, envelope.SustainLevel);
            var rendered = WaveformSampleGenerator.NextSample(waveform, phase) * amplitude;

            // Amplitude modulation (tremolo): multiply by (1 + lfoValue), clamped to non-negative.
            if (lfoTarget == LfoTarget.Amplitude)
            {
                var timeSeconds = i / (double)SampleRate;
                var lfoValue = Lfo.EvaluateSine(lfo!.RateHz, timeSeconds, lfo.Depth);
                var ampMod = Math.Max(0d, 1d + lfoValue);
                rendered *= ampMod;
            }

            // Filter cutoff modulation: recreate the filter every 256 samples (~5.8 ms at 44.1 kHz).
            // Filter IIR state is lost on each recreation, which is a quality compromise accepted
            // to keep the implementation simple. Only active when both filter and LFO are set.
            if (lfoTarget == LfoTarget.FilterCutoff && filter is not null && filter.Type != FilterType.None)
            {
                if (i % 256 == 0)
                {
                    var timeSeconds = i / (double)SampleRate;
                    var lfoValue = Lfo.EvaluateSine(lfo!.RateHz, timeSeconds, lfo.Depth);
                    var modCutoff = filter.CutoffHz * (1 + lfoValue * 0.5);
                    biquad = AudioFilters.Create(filter with { CutoffHz = modCutoff }, SampleRate);
                }
            }

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
