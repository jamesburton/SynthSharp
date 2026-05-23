namespace SynthSharp.Core.Music;

/// <summary>Derives a recommended playable semitone range from a detected pitch.</summary>
public interface IToneRangeEstimator
{
    /// <summary>Returns a <see cref="SampleToneRange"/> for the given pitch estimate, or null when the estimate is unreliable.</summary>
    /// <param name="pitch">Pitch estimate produced by <see cref="IPitchDetector"/>.</param>
    /// <param name="options">Optional tuning; defaults applied when null.</param>
    /// <returns>The recommended tone range, or null when <paramref name="pitch"/> has zero fundamental or insufficient confidence.</returns>
    SampleToneRange? Estimate(PitchEstimate pitch, ToneRangeOptions? options = null);
}
