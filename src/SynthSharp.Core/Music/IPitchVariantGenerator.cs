using SynthSharp.Core.Audio;

namespace SynthSharp.Core.Music;

/// <summary>Generates a set of pitch-shifted variants spanning a sample's recommended tone range.</summary>
public interface IPitchVariantGenerator
{
    /// <summary>
    /// Returns one <see cref="SamplePitchVariant"/> per semitone in
    /// <c>[range.LowSemitone, range.HighSemitone]</c> inclusive, including the un-shifted source at offset 0.
    /// </summary>
    /// <param name="source">The source sample.</param>
    /// <param name="range">The recommended playable range derived from pitch detection.</param>
    /// <param name="shifter">The pitch-shifting implementation to use.</param>
    /// <param name="cancellationToken">Optional token; cancels between iterations.</param>
    /// <returns>The variants, ordered by ascending <see cref="SamplePitchVariant.SemitoneOffset"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/>, <paramref name="range"/>, or <paramref name="shifter"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested mid-generation.</exception>
    IReadOnlyList<SamplePitchVariant> Generate(
        Sample source,
        SampleToneRange range,
        IPitchShifter shifter,
        CancellationToken cancellationToken = default);
}
