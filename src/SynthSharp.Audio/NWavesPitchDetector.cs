using NwPitch = NWaves.Features.Pitch;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Music;

namespace SynthSharp.Audio;

/// <summary>NWaves-backed implementation of <see cref="IPitchDetector"/> using the YIN algorithm.</summary>
public sealed class NWavesPitchDetector : IPitchDetector
{
    /// <inheritdoc/>
    public PitchEstimate Estimate(Sample sample, PitchDetectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (sample.Metadata.FrameCount == 0)
        {
            throw new ArgumentException("Sample has zero frames; nothing to analyse.", nameof(sample));
        }

        var opts = options ?? new PitchDetectionOptions();
        var sampleRate = sample.Metadata.SampleRateHz;
        var mono = DownmixToMono(sample);

        // If the signal is shorter than a single window, NWaves can't run a meaningful YIN.
        // Return a no-detection result rather than throwing — callers expect graceful failure on short clips.
        if (mono.Length < opts.FrameSizeSamples)
        {
            return new PitchEstimate(0f, 0f, Array.Empty<float>());
        }

        var totalFrames = 0;
        var validFrames = 0;
        var perFrame = opts.EmitPerFrameEstimates ? new List<float>() : null;
        var validHz = new List<float>();

        for (var start = 0; start + opts.FrameSizeSamples <= mono.Length; start += opts.HopSizeSamples)
        {
            totalFrames++;
            var hz = NwPitch.FromYin(
                mono,
                sampleRate,
                startPos: start,
                endPos: start + opts.FrameSizeSamples,
                low: opts.MinHz,
                high: opts.MaxHz,
                cmdfThreshold: opts.CmdfThreshold);

            if (hz > 0f)
            {
                validFrames++;
                validHz.Add(hz);
                perFrame?.Add(hz);
            }
            else
            {
                perFrame?.Add(0f);
            }
        }

        if (validHz.Count == 0)
        {
            // No pitch detected in any frame — return empty per-frame list regardless of the flag,
            // giving callers a clean "nothing detected" signal.
            return new PitchEstimate(0f, 0f, Array.Empty<float>());
        }

        validHz.Sort();
        var median = validHz[validHz.Count / 2];
        var confidence = (float)validFrames / totalFrames;

        return new PitchEstimate(median, confidence, (IReadOnlyList<float>)(perFrame ?? new List<float>()));
    }

    private static float[] DownmixToMono(Sample sample)
    {
        if (sample.Metadata.ChannelCount == 1)
        {
            return sample.Channels[0];
        }

        var frameCount = sample.Metadata.FrameCount;
        var channelCount = sample.Metadata.ChannelCount;
        var mono = new float[frameCount];

        for (var f = 0; f < frameCount; f++)
        {
            var sum = 0f;
            for (var c = 0; c < channelCount; c++)
            {
                sum += sample.Channels[c][f];
            }

            mono[f] = sum / channelCount;
        }

        return mono;
    }
}
