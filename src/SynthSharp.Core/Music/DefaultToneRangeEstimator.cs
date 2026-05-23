namespace SynthSharp.Core.Music;

/// <summary>Default <see cref="IToneRangeEstimator"/> — produces a symmetric range around the detected pitch, clamped to the MIDI 0-127 envelope.</summary>
public sealed class DefaultToneRangeEstimator : IToneRangeEstimator
{
    /// <inheritdoc/>
    public SampleToneRange? Estimate(PitchEstimate pitch, ToneRangeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(pitch);

        var opts = options ?? new ToneRangeOptions();

        if (pitch.FundamentalHz <= 0f || pitch.ConfidenceScore < opts.MinConfidence)
        {
            return null;
        }

        var centerMidi = Pitch.ToMidiNote(pitch.FundamentalHz);
        if (centerMidi < 0)
        {
            return null;
        }

        // Clamp the requested semitone bounds so center + offset stays within MIDI [0, 127].
        var clampedLow = -Math.Min(opts.MaxSemitonesBelow, centerMidi);
        var clampedHigh = Math.Min(opts.MaxSemitonesAbove, 127 - centerMidi);

        var noteName = Pitch.ToNoteName(centerMidi);

        return new SampleToneRange(
            CenterNote: noteName,
            CenterPitchHz: pitch.FundamentalHz,
            CenterMidiNote: centerMidi,
            LowSemitone: clampedLow,
            HighSemitone: clampedHigh);
    }
}
