using SynthSharp.Core.Audio;

namespace SynthSharp.Core.Layout;

public sealed class PadAssignment
{
    public required string PadId { get; init; }

    public required int RowIndex { get; init; }

    public required int ColumnIndex { get; init; }

    public required RowRole Role { get; init; }

    public required string KeyBinding { get; set; }

    public required string Label { get; set; }

    public required WaveformType Waveform { get; set; }

    public required double FrequencyHz { get; set; }

    public Envelope Envelope { get; set; } = Envelope.Default;

    /// <summary>
    /// File name of the imported sample (relative to the engine's samples directory).
    /// Null means the pad synthesises via <see cref="Waveform"/> and <see cref="FrequencyHz"/>.
    /// </summary>
    public string? SampleFileName { get; set; }

    /// <summary>
    /// Linear gain multiplier applied to sample data before playback. Default 1.0.
    /// Ignored when <see cref="SampleFileName"/> is null. Out-of-range values are clipped at the PCM16 quantisation stage in the exporter.
    /// </summary>
    public double SampleGain { get; set; } = 1.0;

    /// <summary>Per-pad filter; defaults to <see cref="FilterSettings.Off"/> which bypasses filtering.</summary>
    public FilterSettings Filter { get; set; } = FilterSettings.Off;

    /// <summary>Per-pad LFO; defaults to <see cref="LfoSettings.Off"/> which bypasses modulation.</summary>
    public LfoSettings Lfo { get; set; } = LfoSettings.Off;

    /// <summary>
    /// Optional MIDI note number (0-127) that triggers this pad when a MIDI input device is connected.
    /// Null means no MIDI mapping; the pad can still be triggered by its computer-keyboard binding.
    /// </summary>
    public int? MidiNote { get; set; }

    /// <summary>
    /// When true and <see cref="SampleFileName"/> is set, the sample's loop region is repeated
    /// to fill the engine's maximum sustain duration. Useful for sustained instrument samples
    /// (pads, strings, organs) that would otherwise stop after the source clip's natural length.
    /// </summary>
    public bool SampleLoopEnabled { get; set; }

    /// <summary>
    /// Loop start frame in source-sample space. Defaults to 0 (start of clip). Ignored when
    /// <see cref="SampleLoopEnabled"/> is false.
    /// </summary>
    public int SampleLoopStartFrame { get; set; }

    /// <summary>
    /// Loop end frame in source-sample space (exclusive). 0 means "use the clip's natural end".
    /// Must be greater than <see cref="SampleLoopStartFrame"/> when non-zero. Ignored when
    /// <see cref="SampleLoopEnabled"/> is false.
    /// </summary>
    public int SampleLoopEndFrame { get; set; }
}
