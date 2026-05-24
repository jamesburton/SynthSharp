namespace SynthSharp.Core.Audio;

/// <summary>Per-pad filter configuration applied during rendering.</summary>
/// <param name="Type">The filter shape. <see cref="FilterType.None"/> disables filtering.</param>
/// <param name="CutoffHz">Cutoff (or centre, for BandPass) frequency in Hz.</param>
/// <param name="Resonance">Filter Q factor. 0.707 is Butterworth-flat; higher values produce a resonant peak.</param>
public sealed record FilterSettings(
    FilterType Type,
    double CutoffHz,
    double Resonance)
{
    /// <summary>Default disabled filter — no shaping applied.</summary>
    public static FilterSettings Off { get; } = new(FilterType.None, 1000d, 0.707d);
}
