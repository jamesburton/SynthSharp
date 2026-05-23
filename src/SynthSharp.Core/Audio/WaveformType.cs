namespace SynthSharp.Core.Audio;

/// <summary>Synthesis waveforms supported by <see cref="WaveformSampleGenerator"/>.</summary>
public enum WaveformType
{
    /// <summary>Pure sine wave at the pad's frequency.</summary>
    Sine,

    /// <summary>50% duty-cycle square wave at the pad's frequency.</summary>
    Square,

    /// <summary>Sawtooth wave rising from -1 to +1 over each period.</summary>
    Sawtooth,

    /// <summary>Triangle wave at the pad's frequency.</summary>
    Triangle,

    /// <summary>White noise — frequency-independent random samples. Use with a snappy
    /// envelope (Attack=0, fast Decay, Sustain=0) for percussive hits.</summary>
    Noise,
}
