namespace SynthSharp.Core.Audio;

/// <summary>What an LFO modulates on a pad.</summary>
public enum LfoTarget
{
    /// <summary>No modulation — LFO inactive.</summary>
    None,

    /// <summary>Modulates amplitude (tremolo).</summary>
    Amplitude,

    /// <summary>Modulates pitch (vibrato). Synth pads only; sample pads ignore this target.</summary>
    Pitch,

    /// <summary>Modulates filter cutoff. No-op when the pad's filter is None.</summary>
    FilterCutoff,
}
