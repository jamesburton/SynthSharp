using NWaves.Effects;
using NWaves.Filters.Base;
using NWaves.Operations.Tsm;
using NWaves.Signals;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Music;

namespace SynthSharp.Audio;

/// <summary>
/// NWaves-backed implementation of <see cref="IPitchShifter"/> using
/// <see cref="PitchShiftEffect"/> (phase-vocoder TSM algorithm).
/// <para>
/// Duration is preserved: the phase vocoder shifts pitch without changing length, so
/// <see cref="SampleMetadata.FrameCount"/> is the same as the source's. If the internal
/// vocoder produces a slightly different length (due to hop-size padding), the output is
/// truncated or zero-padded to exactly match the source frame count.
/// </para>
/// </summary>
public sealed class NWavesPitchShifter : IPitchShifter
{
    // Phase-vocoder window / hop defaults — 1024/256 give good pitch accuracy at 44.1 kHz.
    private const int DefaultWindowSize = 1024;
    private const int DefaultHopSize = 256;

    /// <inheritdoc/>
    public Sample Shift(Sample source, int semitones)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Identity: return the source instance directly so callers can Assert.Same.
        if (semitones == 0)
        {
            return source;
        }

        var ratio = Math.Pow(2.0, semitones / 12.0);
        var inputFrameCount = source.Metadata.FrameCount;
        var channelCount = source.Metadata.ChannelCount;

        var outputChannels = new float[channelCount][];

        for (var c = 0; c < channelCount; c++)
        {
            // Fresh effect instance per channel — phase vocoder carries accumulated state
            // that must not bleed between channels.
            var effect = new PitchShiftEffect(ratio, DefaultWindowSize, DefaultHopSize, TsmAlgorithm.PhaseVocoder);

            var inputSignal = new DiscreteSignal(source.Metadata.SampleRateHz, source.Channels[c], allocateNew: false);
            var outputSignal = effect.ApplyTo(inputSignal, FilteringMethod.Auto);

            // Normalise output length to match source frame count: truncate if too long, zero-pad if short.
            outputChannels[c] = NormaliseLength(outputSignal.Samples, inputFrameCount);
        }

        var outputMetadata = source.Metadata with
        {
            FrameCount = inputFrameCount,
            Duration = source.Metadata.Duration,
        };

        return new Sample(outputMetadata, outputChannels);
    }

    /// <summary>
    /// Truncates <paramref name="samples"/> to <paramref name="targetLength"/>, or returns it
    /// directly when the length already matches. Zero-pads when the array is shorter.
    /// </summary>
    /// <param name="samples">The array to normalise.</param>
    /// <param name="targetLength">The desired length.</param>
    /// <returns>A float[] of exactly <paramref name="targetLength"/> elements.</returns>
    private static float[] NormaliseLength(float[] samples, int targetLength)
    {
        if (samples.Length == targetLength)
        {
            return samples;
        }

        var result = new float[targetLength];

        // Copy only as many frames as are available; the rest remain 0 (zero-padded).
        var copyCount = Math.Min(samples.Length, targetLength);
        Array.Copy(samples, result, copyCount);
        return result;
    }
}
