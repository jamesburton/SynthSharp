namespace SynthSharp.Core.Music;

/// <summary>Optional tuning parameters for <see cref="IPitchDetector.Estimate"/>.</summary>
/// <param name="MinHz">Lower bound of the pitch search range. Default 50 Hz (just below low E on a bass).</param>
/// <param name="MaxHz">Upper bound of the pitch search range. Default 2000 Hz (above C7).</param>
/// <param name="FrameSizeSamples">Window size for each YIN call. Default 2048 (~46 ms at 44.1 kHz).</param>
/// <param name="HopSizeSamples">Distance between window starts. Default 1024 (50% overlap).</param>
/// <param name="EmitPerFrameEstimates">
/// When true, populate <see cref="PitchEstimate.PerFrameEstimates"/>; otherwise return an empty list.
/// </param>
/// <param name="CmdfThreshold">YIN aperiodicity threshold (paper recommends 0.10–0.15). Default 0.15.</param>
public sealed record PitchDetectionOptions(
    float MinHz = 50f,
    float MaxHz = 2000f,
    int FrameSizeSamples = 2048,
    int HopSizeSamples = 1024,
    bool EmitPerFrameEstimates = false,
    float CmdfThreshold = 0.15f);
