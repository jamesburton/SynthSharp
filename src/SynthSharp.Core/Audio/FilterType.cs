namespace SynthSharp.Core.Audio;

/// <summary>Per-pad filter type.</summary>
public enum FilterType
{
    /// <summary>No filter — sample passes through unchanged.</summary>
    None,

    /// <summary>Low-pass filter: cuts frequencies above <c>CutoffHz</c>.</summary>
    LowPass,

    /// <summary>High-pass filter: cuts frequencies below <c>CutoffHz</c>.</summary>
    HighPass,

    /// <summary>Band-pass filter: keeps frequencies near <c>CutoffHz</c>, attenuates above and below.</summary>
    BandPass,
}
