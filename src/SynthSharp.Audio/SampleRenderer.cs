using NWaves.Filters.BiQuad;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Persistence;

namespace SynthSharp.Audio;

/// <summary>
/// Renders an imported <see cref="Sample"/> into a PCM16 WAV stream playable by
/// <see cref="IAudioPlaybackBackend"/>, with optional gain, ADSR-envelope shaping, and BiQuad filtering.
/// </summary>
public static class SampleRenderer
{
    /// <summary>
    /// Returns a fresh PCM16 WAV <see cref="MemoryStream"/> containing the sample's audio
    /// with gain, envelope, optional filter, and optional LFO modulation applied. The output
    /// stream's position is at 0 and it is owned by the caller (the caller disposes).
    /// </summary>
    /// <param name="sample">The sample to render.</param>
    /// <param name="gain">Linear multiplier applied to every sample value. 1.0 = unchanged.</param>
    /// <param name="envelope">ADSR envelope. Applied as an amplitude curve across the sample's frames.</param>
    /// <param name="exporter">Used to encode the gain+envelope-shaped Sample as PCM16 WAV.</param>
    /// <param name="filter">
    /// Optional per-pad filter applied sample-by-sample after gain and envelope shaping.
    /// A separate filter instance is constructed per channel to prevent state cross-talk.
    /// Pass <see langword="null"/> or <see cref="FilterSettings.Off"/> for bypass.
    /// </param>
    /// <param name="lfo">
    /// Optional per-pad LFO applied during rendering. Supports <see cref="LfoTarget.Amplitude"/>
    /// (tremolo) and <see cref="LfoTarget.FilterCutoff"/> modulation.
    /// <see cref="LfoTarget.Pitch"/> is not supported for sample pads (resampling per chunk is
    /// out of scope); when that target is set the LFO is silently bypassed.
    /// Pass <see langword="null"/> or <see cref="LfoSettings.Off"/> for no modulation.
    /// </param>
    /// <returns>A PCM16 WAV MemoryStream ready for <see cref="IAudioPlaybackBackend.PlayAsync"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="sample"/> or <paramref name="exporter"/> is null.</exception>
    public static MemoryStream Render(
        Sample sample,
        double gain,
        Envelope envelope,
        ISampleExporter exporter,
        FilterSettings? filter = null,
        LfoSettings? lfo = null)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(exporter);

        var frameCount = sample.Metadata.FrameCount;
        var sampleRate = sample.Metadata.SampleRateHz;
        var channelCount = sample.Metadata.ChannelCount;

        var attackSamples = (int)(sampleRate * envelope.AttackSeconds);
        var decaySamples = (int)(sampleRate * envelope.DecaySeconds);
        var releaseSamples = (int)(sampleRate * envelope.ReleaseSeconds);
        var sustainStart = attackSamples + decaySamples;
        var sustainEnd = Math.Max(sustainStart, frameCount - releaseSamples);

        // Determine active LFO target; null, Target.None, and Target.Pitch all mean bypass.
        // Pitch modulation requires per-sample resampling which is out of scope for sample pads.
        var lfoTarget = lfo?.Target ?? LfoTarget.None;
        var activeLfoTarget = lfoTarget is LfoTarget.Amplitude or LfoTarget.FilterCutoff
            ? lfoTarget
            : LfoTarget.None;

        var shaped = new float[channelCount][];
        for (var c = 0; c < channelCount; c++)
        {
            shaped[c] = new float[frameCount];

            // Construct one independent filter instance per channel so IIR delay-line state
            // does not cross-talk between left and right (or additional channels).
            var biquad = filter is not null ? AudioFilters.Create(filter, sampleRate) : null;

            for (var i = 0; i < frameCount; i++)
            {
                var amp = EnvelopeAmplitude(i, attackSamples, decaySamples, sustainStart, sustainEnd, releaseSamples, envelope.SustainLevel);
                var enveloped = (float)(sample.Channels[c][i] * amp * gain);

                // Amplitude modulation (tremolo): multiply by (1 + lfoValue), clamped to non-negative.
                if (activeLfoTarget == LfoTarget.Amplitude)
                {
                    var timeSeconds = i / (double)sampleRate;
                    var lfoValue = Lfo.EvaluateSine(lfo!.RateHz, timeSeconds, lfo.Depth);
                    var ampMod = (float)Math.Max(0d, 1d + lfoValue);
                    enveloped *= ampMod;
                }

                // Filter cutoff modulation: recreate the filter every 256 samples (~5.8 ms at 44.1 kHz).
                // IIR state is lost on each recreation; accepted quality compromise for simplicity.
                // Only active when both filter and LFO are configured.
                if (activeLfoTarget == LfoTarget.FilterCutoff && filter is not null && filter.Type != FilterType.None)
                {
                    if (i % 256 == 0)
                    {
                        var timeSeconds = i / (double)sampleRate;
                        var lfoValue = Lfo.EvaluateSine(lfo!.RateHz, timeSeconds, lfo.Depth);
                        var modCutoff = filter.CutoffHz * (1 + lfoValue * 0.5);
                        biquad = AudioFilters.Create(filter with { CutoffHz = modCutoff }, sampleRate);
                    }
                }

                shaped[c][i] = biquad is not null ? biquad.Process(enveloped) : enveloped;
            }
        }

        var shapedSample = new Sample(sample.Metadata, shaped);
        var stream = new MemoryStream();
        exporter.Export(shapedSample, stream);
        stream.Position = 0;
        return stream;
    }

    // Mirror of WavToneRenderer's EnvelopeAmplitude helper. Keep in sync if WavToneRenderer changes.
    private static double EnvelopeAmplitude(
        int sampleIndex,
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
