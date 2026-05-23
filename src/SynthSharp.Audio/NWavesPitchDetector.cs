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

        // Guard against NWaves issue #88: Pitch.FromYin throws IndexOutOfRangeException when the
        // frame window is too small to detect the lowest requested pitch. A frame of N samples at
        // sampleRate Hz can detect pitches as low as sampleRate/N Hz; below that the cmdf array
        // overruns. We fail fast with a clear message rather than letting the underlying library throw.
        var minSamplesForMinHz = (int)Math.Ceiling(sampleRate / opts.MinHz);
        if (opts.FrameSizeSamples < minSamplesForMinHz)
        {
            throw new ArgumentException(
                $"FrameSizeSamples ({opts.FrameSizeSamples}) is too small to detect pitches as low as "
                + $"{opts.MinHz} Hz at sample rate {sampleRate} Hz. Increase FrameSizeSamples to at least "
                + $"{minSamplesForMinHz}, or raise MinHz. (Guards against NWaves issue #88.)",
                nameof(options));
        }

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
