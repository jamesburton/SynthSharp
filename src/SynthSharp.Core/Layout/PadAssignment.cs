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
}
