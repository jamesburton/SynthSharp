using SynthSharp.Core.Audio;

namespace SynthSharp.Core.Music;

/// <summary>Estimates the fundamental pitch of an in-memory sample.</summary>
public interface IPitchDetector
{
    /// <summary>Runs pitch detection over <paramref name="sample"/> using the given options.</summary>
    /// <param name="sample">Sample to analyse. Stereo samples are downmixed to mono internally.</param>
    /// <param name="options">Optional tuning; defaults applied when null.</param>
    /// <returns>A <see cref="PitchEstimate"/> with the aggregated fundamental and a confidence score.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sample"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sample"/> has zero frames.</exception>
    PitchEstimate Estimate(Sample sample, PitchDetectionOptions? options = null);
}
