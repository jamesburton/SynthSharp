namespace SynthSharp.Core.Audio;

/// <summary>Per-pad LFO configuration applied during rendering.</summary>
/// <param name="Target">Which parameter the LFO modulates. <see cref="LfoTarget.None"/> disables modulation.</param>
/// <param name="RateHz">Modulation frequency in Hz (typical 0.1–20).</param>
/// <param name="Depth">Modulation depth in [0.0, 1.0].</param>
public sealed record LfoSettings(
    LfoTarget Target,
    double RateHz,
    double Depth)
{
    /// <summary>Default disabled LFO.</summary>
    public static LfoSettings Off { get; } = new(LfoTarget.None, 4.0, 0.5);
}
