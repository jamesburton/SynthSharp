namespace SynthSharp.Core.Music;

/// <summary>Tuning knobs for <see cref="IToneRangeEstimator"/>.</summary>
/// <param name="MaxSemitonesBelow">Maximum semitones below the detected pitch to include in the range. Default 12 (one octave).</param>
/// <param name="MaxSemitonesAbove">Maximum semitones above the detected pitch to include in the range. Default 12 (one octave).</param>
/// <param name="MinConfidence">Minimum <see cref="PitchEstimate.ConfidenceScore"/> required to produce a range. Default 0.3.</param>
public sealed record ToneRangeOptions(
    int MaxSemitonesBelow = 12,
    int MaxSemitonesAbove = 12,
    float MinConfidence = 0.3f);
