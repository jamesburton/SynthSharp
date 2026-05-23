using SynthSharp.Core.Audio;

namespace SynthSharp.Core.Music;

/// <summary>Shifts a sample's pitch by a whole number of semitones.</summary>
public interface IPitchShifter
{
    /// <summary>Returns a new <see cref="Sample"/> shifted by <paramref name="semitones"/>.</summary>
    /// <param name="source">The sample to shift. Must not be null.</param>
    /// <param name="semitones">Number of semitones to shift; negative shifts down, positive shifts up, zero returns the source unchanged.</param>
    /// <returns>
    /// A new Sample whose perceived pitch is <c>source × 2^(semitones/12)</c>.
    /// Implementations may or may not preserve duration — see implementation docs.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    Sample Shift(Sample source, int semitones);
}
