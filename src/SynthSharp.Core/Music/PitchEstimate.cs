namespace SynthSharp.Core.Music;

/// <summary>Result of running pitch detection on a sample.</summary>
/// <param name="FundamentalHz">Aggregated fundamental frequency in Hz; 0 when no pitch was detected.</param>
/// <param name="ConfidenceScore">
/// Stability of the per-frame estimates in [0, 1]; ratio of frames with valid pitch to total frames.
/// </param>
/// <param name="PerFrameEstimates">
/// Per-frame pitch values in Hz, ordered by window start. Empty when not requested by
/// <see cref="PitchDetectionOptions.EmitPerFrameEstimates"/>.
/// </param>
public sealed record PitchEstimate(
    float FundamentalHz,
    float ConfidenceScore,
    IReadOnlyList<float> PerFrameEstimates);
