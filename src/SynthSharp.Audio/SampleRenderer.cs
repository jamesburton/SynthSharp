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
    /// <param name="loopEnabled">
    /// When true, the source region <c>[loopStartFrame, effectiveLoopEnd)</c> is repeated to fill
    /// <paramref name="maxOutputFrames"/> frames of output. The envelope is applied across the
    /// whole output (not per-loop) so attack/decay/release stay natural.
    /// </param>
    /// <param name="loopStartFrame">
    /// Loop start frame within the TRIMMED region (i.e. relative to <paramref name="trimStartFrame"/>).
    /// Ignored when <paramref name="loopEnabled"/> is false.
    /// </param>
    /// <param name="loopEndFrame">
    /// Loop end frame (exclusive) within the TRIMMED region; 0 means "use the trimmed region's natural end".
    /// Ignored when <paramref name="loopEnabled"/> is false.
    /// </param>
    /// <param name="maxOutputFrames">
    /// Cap on the output's frame count. 0 means "use the source's natural length" (no looping
    /// extension even if <paramref name="loopEnabled"/> is true — useful for tests that don't
    /// want a long render). Capped to the trimmed length when looping is disabled.
    /// </param>
    /// <param name="velocity">
    /// Linear amplitude scale in [0.0, 1.0]. Applied as an input-gain stage before the filter:
    /// the rendered sample is multiplied by <c>Math.Clamp(velocity, 0f, 1f)</c>. Defaults to
    /// <c>1.0f</c> which is mathematically identical to the no-velocity path.
    /// </param>
    /// <param name="trimStartFrame">
    /// Trim start frame in source-sample space. Defaults to 0 (start of clip). Source frames before
    /// this index are skipped; the trimmed region begins here. Loop bounds are interpreted within
    /// the trimmed region (i.e. relative to this offset).
    /// </param>
    /// <param name="trimEndFrame">
    /// Trim end frame in source-sample space (exclusive). 0 means "use the source's natural end".
    /// Must be greater than <paramref name="trimStartFrame"/> when non-zero. Loop bounds are
    /// interpreted within the trimmed region, not the untrimmed source.
    /// </param>
    /// <returns>A PCM16 WAV MemoryStream ready for <see cref="IAudioPlaybackBackend.PlayAsync"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="sample"/> or <paramref name="exporter"/> is null.</exception>
    /// <exception cref="ArgumentException">When loop or trim bounds are invalid (negative values, or end at or before start when non-zero).</exception>
    public static MemoryStream Render(
        Sample sample,
        double gain,
        Envelope envelope,
        ISampleExporter exporter,
        FilterSettings? filter = null,
        LfoSettings? lfo = null,
        bool loopEnabled = false,
        int loopStartFrame = 0,
        int loopEndFrame = 0,
        int maxOutputFrames = 0,
        float velocity = 1.0f,
        int trimStartFrame = 0,
        int trimEndFrame = 0)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(exporter);

        var frameCount = sample.Metadata.FrameCount;
        var sampleRate = sample.Metadata.SampleRateHz;
        var channelCount = sample.Metadata.ChannelCount;

        // Validate and resolve trim bounds first. The trimmed region narrows the effective source
        // so that all subsequent loop math operates within [trimStartFrame, trimEndFrame).
        // trimEndFrame == 0 is a sentinel meaning "use the source's natural end".
        if (trimStartFrame < 0)
        {
            throw new ArgumentException($"trimStartFrame must be non-negative; got {trimStartFrame}.", nameof(trimStartFrame));
        }

        if (trimEndFrame < 0)
        {
            throw new ArgumentException($"trimEndFrame must be non-negative; got {trimEndFrame}.", nameof(trimEndFrame));
        }

        var effectiveTrimStart = Math.Min(trimStartFrame, frameCount);
        var effectiveTrimEnd = trimEndFrame > 0 ? Math.Min(trimEndFrame, frameCount) : frameCount;

        if (trimEndFrame > 0 && effectiveTrimEnd <= effectiveTrimStart)
        {
            throw new ArgumentException(
                $"trimEndFrame ({trimEndFrame}) must be greater than trimStartFrame ({trimStartFrame}) when non-zero.",
                nameof(trimEndFrame));
        }

        // effectiveLength is the number of frames in the trimmed region; all loop math below
        // uses this instead of frameCount so loop bounds are relative to the trimmed region.
        var effectiveLength = effectiveTrimEnd - effectiveTrimStart;

        // Resolve loop bounds and compute effective output length.
        // loopEndFrame == 0 is a documented sentinel meaning "use the trimmed region's natural end".
        if (loopStartFrame < 0)
        {
            throw new ArgumentException($"loopStartFrame must be non-negative; got {loopStartFrame}.", nameof(loopStartFrame));
        }

        if (loopEndFrame < 0)
        {
            throw new ArgumentException($"loopEndFrame must be non-negative; got {loopEndFrame}.", nameof(loopEndFrame));
        }

        var effectiveLoopStart = Math.Min(loopStartFrame, effectiveLength);
        var effectiveLoopEnd = loopEndFrame > 0 ? Math.Min(loopEndFrame, effectiveLength) : effectiveLength;

        if (loopEnabled && effectiveLoopEnd <= effectiveLoopStart)
        {
            throw new ArgumentException(
                $"loopEndFrame ({loopEndFrame}) must be greater than loopStartFrame ({loopStartFrame}) when looping is enabled.",
                nameof(loopEndFrame));
        }

        var willLoop = loopEnabled && maxOutputFrames > effectiveLength && effectiveLoopEnd > effectiveLoopStart;
        var outputFrames = willLoop ? maxOutputFrames : (maxOutputFrames > 0 ? Math.Min(maxOutputFrames, effectiveLength) : effectiveLength);

        var attackSamples = (int)(sampleRate * envelope.AttackSeconds);
        var decaySamples = (int)(sampleRate * envelope.DecaySeconds);
        var releaseSamples = (int)(sampleRate * envelope.ReleaseSeconds);
        var sustainStart = attackSamples + decaySamples;
        var sustainEnd = Math.Max(sustainStart, outputFrames - releaseSamples);

        // Determine active LFO target; null, Target.None, and Target.Pitch all mean bypass.
        // Pitch modulation requires per-sample resampling which is out of scope for sample pads.
        var lfoTarget = lfo?.Target ?? LfoTarget.None;
        var activeLfoTarget = lfoTarget is LfoTarget.Amplitude or LfoTarget.FilterCutoff
            ? lfoTarget
            : LfoTarget.None;

        var shaped = new float[channelCount][];
        for (var c = 0; c < channelCount; c++)
        {
            shaped[c] = new float[outputFrames];

            // Construct one independent filter instance per channel so IIR delay-line state
            // does not cross-talk between left and right (or additional channels).
            var biquad = filter is not null ? AudioFilters.Create(filter, sampleRate) : null;

            for (var i = 0; i < outputFrames; i++)
            {
                // Resolve source frame within the trimmed region: first play 0..effectiveLoopEnd
                // (in trimmed-region space), then cycle through [effectiveLoopStart, effectiveLoopEnd).
                // srcIdxInTrimmed is in [0, effectiveLength); translate to source space by adding
                // effectiveTrimStart so we read from the correct position in the raw channel data.
                int srcIdxInTrimmed;
                if (i < effectiveLoopEnd || !willLoop)
                {
                    srcIdxInTrimmed = Math.Min(i, effectiveLength - 1);
                }
                else
                {
                    var loopLen = effectiveLoopEnd - effectiveLoopStart;
                    srcIdxInTrimmed = effectiveLoopStart + ((i - effectiveLoopEnd) % loopLen);
                }

                var srcIdx = srcIdxInTrimmed + effectiveTrimStart;

                var amp = EnvelopeAmplitude(i, attackSamples, decaySamples, sustainStart, sustainEnd, releaseSamples, envelope.SustainLevel);
                var enveloped = (float)(sample.Channels[c][srcIdx] * amp * gain);

                // Amplitude modulation (tremolo): multiply by (1 + lfoValue), clamped to non-negative.
                if (activeLfoTarget == LfoTarget.Amplitude)
                {
                    var timeSeconds = i / (double)sampleRate;
                    var lfoValue = Lfo.EvaluateSine(lfo!.RateHz, timeSeconds, lfo.Depth);
                    var ampMod = (float)Math.Max(0d, 1d + lfoValue);
                    enveloped *= ampMod;
                }

                // Velocity: scale by clamped [0, 1] before the filter so velocity behaves as an
                // input-gain stage. When velocity == 1.0f this is a no-op (1.0f multiply is exact).
                enveloped *= Math.Clamp(velocity, 0f, 1f);

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

        // Build fresh metadata when outputFrames differs from the source's frameCount — this
        // covers both loop extension (outputFrames > frameCount) and trim shortening
        // (outputFrames < frameCount). The Sample invariant channels[i].Length == FrameCount
        // must always hold.
        var shapedMetadata = outputFrames == frameCount
            ? sample.Metadata
            : sample.Metadata with
            {
                FrameCount = outputFrames,
                Duration = TimeSpan.FromSeconds(outputFrames / (double)sampleRate),
            };

        var shapedSample = new Sample(shapedMetadata, shaped);
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
