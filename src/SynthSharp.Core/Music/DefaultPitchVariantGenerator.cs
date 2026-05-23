using SynthSharp.Core.Audio;

namespace SynthSharp.Core.Music;

/// <summary>
/// Default <see cref="IPitchVariantGenerator"/> that iterates from
/// <see cref="SampleToneRange.LowSemitone"/> to <see cref="SampleToneRange.HighSemitone"/> inclusive
/// and produces one variant per semitone.
/// </summary>
/// <remarks>
/// Precondition: the combined MIDI note (<see cref="SampleToneRange.CenterMidiNote"/> + offset) must
/// remain in [0, 127] for every offset in the range, or <see cref="Pitch.ToNoteName"/> will throw
/// <see cref="ArgumentOutOfRangeException"/>. The tone range should have been clamped at estimation
/// time (e.g., by <c>DefaultToneRangeEstimator</c>) to satisfy this constraint.
/// </remarks>
public sealed class DefaultPitchVariantGenerator : IPitchVariantGenerator
{
    /// <inheritdoc/>
    public IReadOnlyList<SamplePitchVariant> Generate(
        Sample source,
        SampleToneRange range,
        IPitchShifter shifter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(shifter);

        if (range.HighSemitone < range.LowSemitone)
        {
            throw new ArgumentException(
                $"Tone range high ({range.HighSemitone}) is below low ({range.LowSemitone}).",
                nameof(range));
        }

        var variants = new List<SamplePitchVariant>();

        for (var offset = range.LowSemitone; offset <= range.HighSemitone; offset++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var shifted = shifter.Shift(source, offset);
            var midi = range.CenterMidiNote + offset;
            var noteName = Pitch.ToNoteName(midi);
            var frequencyHz = (float)Pitch.ToFrequencyHz(midi);

            variants.Add(new SamplePitchVariant(
                Sample: shifted,
                SemitoneOffset: offset,
                Note: noteName,
                MidiNote: midi,
                FrequencyHz: frequencyHz));
        }

        return variants;
    }
}
